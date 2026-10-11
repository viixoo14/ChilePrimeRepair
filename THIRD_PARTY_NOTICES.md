# Componentes de terceros

El código propio de Chile Prime Repair se distribuye con licencia MIT. Los siguientes componentes conservan sus licencias originales.

| Componente | Versión o procedencia | Licencia y avisos |
| --- | --- | --- |
| Montserrat Regular, Medium, SemiBold y Bold | Archivos TrueType sin modificar, revisión `555facfb2a18c72c3c0380f0d9c0f060453a9058` de `JulietaUla/Montserrat` | SIL Open Font License 1.1, en `src/ChilePrimeRepair.App/Assets/Fonts/Montserrat/OFL.txt`; el ZIP portátil incluye `OFL.txt` y `FONT-SOURCES.md`. |
| .NET Runtime para Windows x64 | Paquete `Microsoft.NETCore.App.Runtime.win-x64` 10.0.12, utilizado por el SDK fijado | Licencia MIT en `licenses/dotnet/NET-RUNTIME-LICENSE.txt` y avisos de sus componentes en `licenses/dotnet/NET-RUNTIME-THIRD-PARTY-NOTICES.txt`. |
| .NET Windows Desktop Runtime / WPF | Paquete `Microsoft.WindowsDesktop.App.Runtime.win-x64` 10.0.12 | Licencia MIT en `licenses/dotnet/WINDOWS-DESKTOP-LICENSE.txt`. |

Los textos de .NET incluidos se copiaron sin modificar desde los paquetes oficiales de runtime. El SDK es una herramienta de desarrollo y no se incluye como tal en el ZIP del corrector. Si se actualiza el SDK o la versión del runtime, deben actualizarse también sus licencias y avisos.

Montserrat se incorpora al ejecutable y no se instala en Windows. La aplicación invoca las API de Windows para procesos, registro, accesos directos y ventanas; no redistribuye el sistema operativo.

FiveM y GTA V son programas externos del jugador. Este repositorio no contiene sus ejecutables, recursos de servidores, archivos de configuración de jugadores ni el CSS de referencia. Los enlaces al código de Cfx.re documentan el ajuste de NUI y la detección de instalación; no incorporan ese código al proyecto.
