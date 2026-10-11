# Contribuir

Instala en Windows el SDK indicado en `global.json` y ejecuta `powershell -File .\build.ps1`. Las pruebas crean instalaciones ficticias; no apuntes los modos de prueba a tu instalación real.

Envía los cambios mediante una pull request. Describe el comportamiento afectado y las comprobaciones realizadas. Las contribuciones se publican bajo la licencia MIT del proyecto; los componentes de terceros conservan sus propias licencias.

Las modificaciones de rutas de borrado, respaldos, restauración, detección de instalaciones y scripts de compilación requieren especial revisión. Las correcciones no deben ampliar la lista de carpetas de caché permitidas, alterar las protecciones de Windows ni ejecutar accesos directos encontrados durante la detección.

No adjuntes credenciales, archivos `fivem.cfg` de jugadores, respaldos completos ni rutas que identifiquen a personas. Para reproducir un problema, utiliza una instalación simulada.

Los cambios se revisan antes de integrarse. Cada versión que se solicite firmar debe identificar el commit y pasar las pruebas en GitHub Actions. El responsable debe aprobar la solicitud de producción en SignPath.
