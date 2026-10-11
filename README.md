# Asistente de Chile Prime · versión 0.4.0

Aplicación portátil para Windows 10/11 de 64 bits que limpia la caché del cliente FiveM y activa el tamaño fijo de las interfaces NUI a 1920 × 1080. Funciona localmente, sin cuentas, sin telemetría propia y sin envío de archivos a un servidor.

Código abierto bajo [licencia MIT](LICENSE). La solicitud de firma gratuita fue enviada a SignPath Foundation y está **pendiente de evaluación, sin aprobación ni una versión firmada disponible**. Consulta [Code signing policy](CODE_SIGNING.md) y [Privacidad](PRIVACY.md).

[Descargas y resultados de pruebas](https://github.com/viixoo14/ChilePrimeRepair/releases). **Sin firma digital:** Windows puede bloquear el ejecutable. La descarga incluye hashes y el informe de pruebas.

## Uso

1. Extrae el ZIP y abre **ChilePrimeRepair.exe**. El ejecutable incluye su entorno .NET: no necesitas instalar .NET ni ejecutarlo como administrador.
2. En la pantalla inicial verás la marca Chile Prime y el botón **Comenzar**. La parte superior usa el mismo fondo oscuro, con controles discretos para minimizar, maximizar/restaurar y cerrar. Arrastra el espacio libre de esa zona para mover la ventana.
3. Pulsa **Comenzar**. El asistente detectará FiveM automáticamente. Antes de modificar archivos, muestra durante seis segundos la limpieza de caché, el tamaño fijo de las interfaces y los ajustes adicionales seleccionados según el diagnóstico. Puedes cerrar la ventana para cancelar.
4. Si FiveM o GTA V están abiertos, ciérralos cuando el asistente lo indique. Continuará automáticamente.
5. La instalación se busca automáticamente en procesos abiertos, el registro de Windows, la carpeta habitual, los accesos directos y ubicaciones comunes de discos locales. No se pide elegir una carpeta. Si no se encuentra, se ofrece **Reintentar**: abre FiveM una vez y vuelve a probar.
6. Espera a que muestre **Reparación completada** y pulsa **Finalizar**. Después abre FiveM y prueba Chile Prime. Los recursos eliminados de la caché se descargarán de nuevo cuando sean necesarios.

Durante el trabajo solo se muestran la etapa actual y el progreso. Si hay un problema, se explica al terminar y se ofrece **Reintentar**. Si no se detecta una instalación válida, no se modifican archivos. Cerrar el asistente solicita que se interrumpa la operación antes de salir; si la limpieza ya comenzó, algunos archivos pueden haberse eliminado.

Al terminar, **Deshacer los ajustes** recupera los valores anteriores a la última reparación si existe un respaldo válido. Conserva preferencias y teclas que hayas cambiado después. Si modificaste manualmente alguno de los ajustes reparados, se bloquea la restauración para conservar tu cambio. La restauración no recupera los archivos de caché eliminados.

## Cambios exactos

- La instalación debe contener `CitizenFX.ini` y una carpeta `citizen` o un archivo `components.json`. Se aceptan ubicaciones personalizadas.
- La limpieza se limita al contenido de `FiveM.app\data\cache`, `FiveM.app\data\server-cache` y `FiveM.app\data\server-cache-priv`. Las carpetas raíz se conservan.
- Se conservan `game-storage`, cualquier subcarpeta llamada `game`, `nui-storage`, registros, plugins, credenciales y archivos de configuración fuera de esas tres carpetas.
- Se activa `nui_useFixedSize` en `%AppData%\CitizenFX\fivem.cfg`. Se conserva la codificación del archivo, los comentarios y las demás preferencias.
- Los respaldos quedan en `%LocalAppData%\ChilePrimeRepair\backups`. Cada uno contiene un manifiesto y, si existía, una copia íntegra de la configuración anterior.
- La escala de Windows y la resolución del juego se conservan. La opción de compatibilidad gráfica NUI solo cambia cuando se detecta el fallo específico descrito abajo.

La presencia de caché por sí sola no demuestra que esté dañada: la limpieza es una reparación solicitada por el jugador. Completar la reparación no certifica que el problema dentro del servidor se haya solucionado. El resultado de una limpieza interrumpida identifica cuántos archivos alcanzaron a eliminarse.

## Protección de archivos

Antes de limpiar se comprueba que FiveM esté cerrado y que los archivos de caché no estén bloqueados ni sean de solo lectura. Las rutas de limpieza se validan contra una lista fija. Se rechazan enlaces, junctions y carpetas redirigidas. Las escrituras de configuración son atómicas y se detienen si otra aplicación cambió el archivo. Un respaldo modificado no se restaura.

## Diagnóstico y correcciones según la evidencia

La versión 0.4.0 conserva la pantalla inicial con logo y Comenzar. Después de cerrar el juego analiza el equipo y la instalación antes del aviso de cambios. El resultado ofrece **Ver diagnóstico y recomendaciones**, **Copiar informe para soporte** y **Deshacer los ajustes**.

- Lee RAM total y disponible mediante Windows, adaptadores y memoria gráfica mediante DXGI, espacio libre de la unidad y preferencias gráficas guardadas para los ejecutables locales de FiveM/GTA. La memoria compartida es un máximo, no VRAM dedicada. Esta lectura con el juego cerrado no mide consumo en una partida ni confirma la GPU activa.
- Lee como máximo tres registros `CitizenFX_log*.log` de los últimos siete días, hasta 256 KiB de la parte final de cada archivo. Clasifica señales específicas de falta de memoria, fallos del proceso gráfico de CEF/NUI, descarga y colisiones. La falta de coincidencias se presenta como diagnóstico inconcluso, no como una instalación sana o corrupta.
- Si hay errores de memoria y `str_maxVehicleTextureRes` está por encima de 1024, prueba el valor estándar 1024. Puede reducir calidad visual; no se aplica solo por el modelo de GPU.
- Ante un fallo específico de lanzamiento o cierre inesperado del proceso GPU de CEF/NUI, y sin señales de falta de memoria, prueba `nui_useInProcessGpu=true`. Requiere reiniciar FiveM y puede reducir la fluidez. Los valores de formato inesperado se conservan.
- Lee `vid_budgetScale` y ofrece recomendaciones sobre presupuesto y calidad de texturas cuando hay señales de memoria o colisiones. **No cambia este presupuesto automáticamente:** no se ha calibrado un perfil de Chile Prime con jugadores, y la VRAM nominal por sí sola no permite obtener un valor óptimo.
- Detecta `production`, `beta` y `canary` en `CitizenFX.ini`; recomienda Release/Production para estabilidad si el canal es experimental. No cambia el canal ni fuerza versiones del juego.
- Si hay varios adaptadores, ofrece abrir **Gráficos de Windows** para revisar la preferencia del proceso del juego. No cambia el registro ni asegura que la GPU dedicada esté activa.
- RAM disponible inferior a 2 GiB y espacio libre inferior a 5 GiB generan avisos orientativos. Son umbrales de diagnóstico del asistente, no requisitos oficiales de FiveM ni una predicción de rendimiento.

El informe se guarda localmente en `%LocalAppData%\ChilePrimeRepair\reports`. No guarda los registros originales, direcciones de red, rutas del usuario ni identificadores de hardware, y no se sube automáticamente. Puedes copiarlo y añadir el síntoma, el lugar del mapa y si ocurre a otros jugadores antes de enviarlo a soporte.

El aviso previo enumera los cambios seleccionados. La configuración se vuelve a comprobar antes de limpiar: si cambió después del diagnóstico, la operación se detiene. Deshacer recupera únicamente los ajustes modificados por la última reparación, conserva preferencias posteriores y se bloquea si el jugador cambió manualmente cualquiera de esos ajustes. Los respaldos de 0.3.3 siguen siendo compatibles.

## Estado de esta versión

Versión de prueba 0.4.0, sin firma digital. Sus pruebas deben comprobarse en GitHub Actions antes de publicar una nueva descarga. La mejora de FPS, interfaces y colisiones debe validarse dentro de Chile Prime; las pruebas de desarrollo utilizan instalaciones ficticias y no demuestran resultados en partidas reales.

Si varios jugadores caen en el mismo lugar, debe investigarse el recurso del mapa o sus colisiones. El corrector no modifica los recursos del servidor. La caché del cliente puede incluir recursos de otros servidores FiveM.

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
