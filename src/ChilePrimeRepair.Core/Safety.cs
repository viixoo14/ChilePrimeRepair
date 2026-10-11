using System.Security.Cryptography;

namespace ChilePrimeRepair.Core;

public static class Safety
{
    public static string FullPath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static void EnsureBelow(string path, string root)
    {
        var full = FullPath(path);
        var parent = FullPath(root);
        if (!full.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("La ruta está fuera de la carpeta autorizada. No se modificó ese archivo.");
    }

    // Never follow junctions, symbolic links or other reparse points during a mutation.
    public static void EnsureNoLinks(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("La ruta contiene un enlace o una carpeta redirigida. Selecciona una instalación directa.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var next = Path.GetDirectoryName(current);
            if (next == current) break;
            current = next ?? "";
        }
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string? FileHash(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;

    public static void AtomicWrite(string path, byte[] bytes, string? expectedHash)
    {
        EnsureNoLinks(path);
        var parent = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, ".prime-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            EnsureNoLinks(path);
            if (FileHash(path) != expectedHash)
                throw new IOException("La configuración cambió mientras se preparaba la reparación. Vuelve a analizar.");
            if (expectedHash is null)
                File.Move(temporary, path, false);
            else
                File.Replace(temporary, path, null);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
