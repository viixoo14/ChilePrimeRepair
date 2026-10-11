# Preparar y activar SignPath

Estado inicial: solicitud en preparación. Este documento no representa una aprobación de SignPath ni una firma ya contratada.

1. Publicar el repositorio con MIT, README, política de privacidad y Code signing policy. Crear la primera versión de prueba únicamente después de revisar sus resultados. Describir claramente cualquier binario inicial sin firma y las limitaciones de validación.
2. Presentar [la solicitud de SignPath Foundation](https://signpath.org/apply.html) con el repositorio público, página de descarga y datos reales del responsable. Declarar que es un proyecto nuevo y no atribuirle usuarios, descargas, reputación o respaldo oficial que no tenga. La aceptación depende de SignPath.
3. El responsable debe revisar y aceptar personalmente las condiciones de la solicitud y el tratamiento de sus datos. Los colaboradores deben habilitar MFA en GitHub y SignPath. La firma no se activa mientras esto esté pendiente.
4. Después de la aprobación, vincular el repositorio al sistema de compilación GitHub.com de SignPath. Conceder acceso solo al repositorio del corrector. Revisar los permisos que solicite la integración antes de aceptarlos.
5. Crear o adaptar el proyecto `ChilePrimeRepair`, la política de producción `release` y la configuración `portable-windows-x64`. La política debe exigir aprobación manual de Valentino Bluscel y permitir únicamente compilaciones de `main` en runners alojados por GitHub. Importar `.signpath/artifact-configuration.xml` y comprobar sus restricciones.
6. Guardar la credencial de envío limitada al proyecto como secreto de GitHub Actions `SIGNPATH_API_TOKEN`. Configurar la variable `SIGNPATH_ORGANIZATION_ID` con el identificador asignado. Estos valores se obtienen después del alta; no deben enviarse al chat ni incluirse en el repositorio.
7. Cuando los pasos anteriores estén verificados, establecer `SIGNPATH_ENABLED` en `true`. Ejecutar manualmente `Request SignPath signature` desde `main` y aprobar la solicitud en SignPath. El workflow espera hasta veinte minutos; coordinar la aprobación con el responsable.
8. El workflow verifica la firma de SignPath Foundation y su sello de tiempo, prueba el ejecutable firmado con fixtures y genera el hash del EXE final. Publicar solo el artefacto que haya aprobado estas comprobaciones y revisar el comportamiento en equipos reales antes de afirmar que mejora FPS o corrige problemas del servidor.
9. Actualizar el estado del README, la política de firma y las notas de descarga. Incluir como acreditación efectiva: `Free code signing provided by SignPath.io, certificate by SignPath Foundation.` No debe anunciarse como un servicio ya activo antes de la aprobación.

Las actions de compilación y firma están fijadas a commits completos. Las actualizaciones de esas referencias y de scripts de compilación deben revisarse como cualquier cambio de código.

Referencias: [requisitos de la Foundation](https://signpath.org/terms.html), [integración con GitHub](https://docs.signpath.io/trusted-build-systems/github), [configuración de artefactos](https://docs.signpath.io/artifact-configuration/syntax) y [restricciones de metadatos](https://docs.signpath.io/artifact-configuration/reference).
