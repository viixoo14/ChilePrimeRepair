# Asistente de Chile Prime · versión 0.3.3

Aplicación portátil para Windows 10/11 de 64 bits que limpia la caché del cliente FiveM y activa el tamaño fijo de las interfaces NUI a 1920 × 1080. Funciona localmente, sin cuentas, sin telemetría propia y sin envío de archivos a un servidor.

Código abierto bajo [licencia MIT](LICENSE). La firma gratuita de SignPath Foundation está **en preparación, sin aprobación ni una versión firmada disponible**. Consulta [Code signing policy](CODE_SIGNING.md) y [Privacidad](PRIVACY.md).

[Descargar la versión de prueba 0.3.3](https://github.com/viixoo14/ChilePrimeRepair/releases/tag/v0.3.3-preview). **Sin firma digital:** Windows puede bloquear el ejecutable. La descarga incluye hashes y el informe de pruebas.

## Uso

1. Extrae el ZIP y abre **ChilePrimeRepair.exe**. El ejecutable incluye su entorno .NET: no necesitas instalar .NET ni ejecutarlo como administrador.
2. En la pantalla inicial verás la marca Chile Prime y el botón **Comenzar**. La parte superior usa el mismo fondo oscuro, con controles discretos para minimizar, maximizar/restaurar y cerrar. Arrastra el espacio libre de esa zona para mover la ventana.
3. Pulsa **Comenzar**. El asistente detectará FiveM automáticamente. Antes de modificar archivos, muestra durante seis segundos los cambios que realizará: borrar caché y fijar el tamaño de las interfaces. Puedes cerrar la ventana para cancelar.
4. Si FiveM o GTA V están abiertos, ciérralos cuando el asistente lo indique. Continuará automáticamente.
5. La instalación se busca automáticamente en procesos abiertos, el registro de Windows, la carpeta habitual, los accesos directos y ubicaciones comunes de discos locales. No se pide elegir una carpeta. Si no se encuentra, se ofrece **Reintentar**: abre FiveM una vez y vuelve a probar.
6. Espera a que muestre **Reparación completada** y pulsa **Finalizar**. Después abre FiveM y prueba Chile Prime. Los recursos eliminados de la caché se descargarán de nuevo cuando sean necesarios.

Durante el trabajo solo se muestran la etapa actual y el progreso. Si hay un problema, se explica al terminar y se ofrece **Reintentar**. Si no se detecta una instalación válida, no se modifican archivos. Cerrar el asistente solicita que se interrumpa la operación antes de salir; si la limpieza ya comenzó, algunos archivos pueden haberse eliminado.

Al terminar, **Deshacer el ajuste de interfaces** recupera el valor anterior a la última reparación si existe un respaldo válido. Conserva preferencias y teclas que hayas cambiado después. Si modificaste manualmente el propio ajuste NUI, se bloquea la restauración para conservar tu cambio. La restauración no recupera los archivos de caché eliminados.

## Cambios exactos

- La instalación debe contener `CitizenFX.ini` y una carpeta `citizen` o un archivo `components.json`. Se aceptan ubicaciones personalizadas.
- La limpieza se limita al contenido de `FiveM.app\data\cache`, `FiveM.app\data\server-cache` y `FiveM.app\data\server-cache-priv`. Las carpetas raíz se conservan.
- Se conservan `game-storage`, cualquier subcarpeta llamada `game`, `nui-storage`, registros, plugins, credenciales y archivos de configuración fuera de esas tres carpetas.
- Se activa únicamente `nui_useFixedSize` en `%AppData%\CitizenFX\fivem.cfg`. Se conserva la codificación del archivo, los comentarios y las demás preferencias.
- Los respaldos quedan en `%LocalAppData%\ChilePrimeRepair\backups`. Cada uno contiene un manifiesto y, si existía, una copia íntegra de la configuración anterior.
- La escala de Windows, la resolución del juego y las opciones de procesamiento de GPU de NUI se conservan.

La presencia de caché por sí sola no demuestra que esté dañada: la limpieza es una reparación solicitada por el jugador. Completar la reparación no certifica que el problema dentro del servidor se haya solucionado. El resultado de una limpieza interrumpida identifica cuántos archivos alcanzaron a eliminarse.

## Protección de archivos

Antes de limpiar se comprueba que FiveM esté cerrado y que los archivos de caché no estén bloqueados ni sean de solo lectura. Las rutas de limpieza se validan contra una lista fija. Se rechazan enlaces, junctions y carpetas redirigidas. Las escrituras de configuración son atómicas y se detienen si otra aplicación cambió el archivo. Un respaldo modificado no se restaura.

## Estado de esta versión

La versión 0.3.3 prepara el proyecto para publicarlo como código abierto y solicitar firma a SignPath Foundation. Incorpora un aviso de cambios antes de modificar archivos, pruebas de cancelación durante ese aviso, licencias de los componentes redistribuidos y workflows de GitHub Actions. La bienvenida conserva el logo y el botón Comenzar.

La [compilación de GitHub Actions](https://github.com/viixoo14/ChilePrimeRepair/actions/runs/38101544514) finalizó correctamente: **42 pruebas del núcleo aprobadas, cero fallos**, y prueba del asistente aprobada. Corresponde al commit `097d818e03c92ad290f12daf0cd7432c74fb595b`, con el SDK .NET 10.0.401 y un runner Windows alojado por GitHub. La descarga portátil conserva el artefacto original de esa ejecución. No se ha cambiado la protección de Windows ni se han modificado archivos reales de jugadores.

Las pruebas del asistente verificaron Montserrat incorporada, bienvenida, controles de ventana, progreso, reparación, restauración, reintento y cancelación. El aviso aparece antes de escribir y cerrar durante el aviso conserva los archivos. La detección utiliza instalaciones ficticias; la nitidez en distintos monitores y la detección en instalaciones reales deben verificarse con jugadores antes de una versión de producción.

Versión de preparación para pruebas con jugadores. El binario todavía no tiene firma digital. Las pruebas aprobadas cubren limpieza y restauración con instalaciones simuladas. La mejora de FPS, las interfaces y las colisiones del mapa deben validarse dentro de Chile Prime con casos reales, incluyendo escalas de 100%, 125%, 150% y 200%, resoluciones normales y pantallas ultrawide.

La caché de estas carpetas puede contener recursos de otros servidores FiveM: la limpieza actúa sobre la caché del cliente, no identifica exclusivamente archivos de Chile Prime.

## Desarrollo

- C# / .NET 10, interfaz WPF. Sin paquetes externos de aplicación.
- Instala en Windows el SDK de .NET **10.0.401**, fijado en `global.json`, para compilar el código fuente.
- Ejecuta `powershell -File .\build.ps1` para correr las pruebas del núcleo, publicar una versión portátil x64 y probar el asistente. La compilación queda en `artifacts\unsigned`; los resultados, en `artifacts\test-results`.
- Si Windows bloquea la ejecución de pruebas por requisitos de firma, el script se detiene. No se deben presentar esos resultados como pruebas aprobadas.
- También puedes usar `dotnet run --project tests\ChilePrimeRepair.Tests -c Release` y `dotnet run --project src\ChilePrimeRepair.App -c Release`.
- Las pruebas usan instalaciones ficticias en una carpeta temporal y nunca leen la configuración real de FiveM.
- El modo `ChilePrimeRepair.exe --smoke-test CARPETA` verifica el asistente con una instalación ficticia nueva dentro de esa carpeta. El modo `--preview IMAGEN.png --scale 1.5` muestra la bienvenida; añade `--state progress`, `--state waiting` o `--state success` para las otras etapas. No modifica FiveM.
- El porcentaje representa etapas del trabajo. Durante la limpieza se calcula con las entradas de caché procesadas y no estima el tiempo restante. El 100% final solo se alcanza tras una reparación exitosa.

## Code signing policy

Consulta [la política completa de firma](CODE_SIGNING.md). El workflow `Build and verify` compila y prueba el código público en un runner Windows alojado por GitHub. El workflow manual `Request SignPath signature` permanece inactivo hasta la aprobación de SignPath y su configuración; requiere aprobación manual de producción en el servicio. No se solicita firma desde pull requests.

Las compilaciones iniciales deben identificarse como **sin firma**. Obtener una firma válida no certifica que todos los problemas de Chile Prime se resuelvan ni garantiza la aceptación del proyecto por SignPath.

## Licencias y mantenimiento

El código propio utiliza MIT. Montserrat conserva SIL Open Font License 1.1 y el entorno .NET sus licencias y avisos originales; véase [Componentes de terceros](THIRD_PARTY_NOTICES.md). Para contribuir, lee [CONTRIBUTING.md](CONTRIBUTING.md). Para retirar el programa o recuperar el ajuste, lee [UNINSTALL.md](UNINSTALL.md).

## Diseño de la interfaz

El asistente presenta marca, botón Comenzar y progreso automático, usando Montserrat en la marca y los textos de la interfaz. Los fondos grafito y acentos azules están centralizados en `App.xaml`. La composición tipográfica Chile Prime toma como referencia la marca del [sitio del servidor](https://chileprime.net/).

Se incluyen las variantes TrueType Regular, Medium, SemiBold y Bold de [Montserrat](https://github.com/JulietaUla/Montserrat) dentro del ejecutable, sin instalar fuentes en Windows. Se distribuyen con su licencia SIL Open Font License 1.1 en `Assets/Fonts/Montserrat/OFL.txt`. El archivo `FONT-SOURCES.md` identifica la revisión original utilizada.

## Referencias

- [Marco y controles personalizados de ventanas WPF](https://learn.microsoft.com/en-us/dotnet/api/system.windows.shell.windowchrome).
- [Supresión del borde de DWM en Windows 11](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute).
- [ClearType, fondos opacos y efectos de WPF](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.cleartypehint).
- [Calidad del texto estático de WPF](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.texthintingmode).
- [Registro y accesos directos de la instalación FiveM](https://github.com/citizenfx/fivem/blob/master/code/client/launcher/Installer.cpp).
- [Lectura nativa de accesos directos de Windows](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishelllinkw-getpath).
- [Tamaño fijo de NUI: código fuente de FiveM](https://github.com/citizenfx/fivem/blob/master/code/components/nui-core/src/NUIWindow.cpp).
- [Anuncio de compatibilidad de NUI a 1920 × 1080](https://forum.cfx.re/t/fix-nui-gsclient-further-improve-invalid-dynamic-json-data-handling/4945267).
- [Configuración persistente de comandos FiveM](https://docs.fivem.net/docs/client-manual/console-commands/).

Esta aplicación es una utilidad independiente para el cliente FiveM. No se presenta como un producto oficial de Cfx.re.
