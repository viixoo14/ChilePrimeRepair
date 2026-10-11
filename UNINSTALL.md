# Retirar el corrector y recuperar el ajuste

El programa es portátil: no instala servicios, tareas programadas ni fuentes en Windows. Para retirarlo, ciérralo y elimina su carpeta extraída.

Si quieres recuperar las preferencias previas, usa **Deshacer los ajustes** antes de retirar el corrector. La restauración recupera únicamente los ajustes modificados por la última reparación: `nui_useFixedSize` y, cuando correspondía, `str_maxVehicleTextureRes` y `nui_useInProcessGpu`. Conserva otras preferencias y se detiene si modificaste manualmente alguno de esos ajustes o el respaldo no es válido. Reconoce los respaldos de la versión 0.3.3.

Los respaldos locales permanecen en `%LocalAppData%\ChilePrimeRepair\backups`. Puedes conservarlos o eliminar esa carpeta después de decidir que ya no los necesitas. Borrarla elimina la posibilidad de restaurar el valor anterior mediante el asistente.

La restauración no recupera la caché borrada: FiveM descarga los recursos necesarios al utilizarlos de nuevo.

Los informes locales permanecen en `%LocalAppData%\ChilePrimeRepair\reports`. Puedes eliminar esa carpeta para retirar los informes; hacerlo no afecta los respaldos de configuración.
