# Retirar el corrector y recuperar el ajuste

El programa es portátil: no instala servicios, tareas programadas ni fuentes en Windows. Para retirarlo, ciérralo y elimina su carpeta extraída.

Si quieres recuperar el ajuste de interfaces previo, usa **Deshacer el ajuste de interfaces** antes de retirar el corrector. La restauración recupera únicamente `nui_useFixedSize`, conserva otras preferencias y se detiene si modificaste manualmente ese ajuste o el respaldo no es válido.

Los respaldos locales permanecen en `%LocalAppData%\ChilePrimeRepair\backups`. Puedes conservarlos o eliminar esa carpeta después de decidir que ya no los necesitas. Borrarla elimina la posibilidad de restaurar el valor anterior mediante el asistente.

La restauración no recupera la caché borrada: FiveM descarga los recursos necesarios al utilizarlos de nuevo.
