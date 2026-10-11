# Code signing policy

**Estado: preparación de una solicitud a SignPath Foundation. No hay aprobación ni una versión firmada todavía.**

El proyecto solicita firma gratuita para software de código abierto a [SignPath Foundation](https://signpath.org/terms.html). Publicar este archivo no implica que SignPath haya aceptado el proyecto o proporcione actualmente un certificado.

## Responsables

- Autor y mantenedor: Valentino Bluscel ([viixoo14 en GitHub](https://github.com/viixoo14)), responsable de Chile Prime Repair.
- Revisión de contribuciones: Valentino Bluscel ([viixoo14](https://github.com/viixoo14)). Los cambios de colaboradores se revisan antes de integrarse.
- Aprobación de versiones para firma: Valentino Bluscel ([viixoo14](https://github.com/viixoo14)). Cada solicitud de producción requiere aprobación manual en SignPath.

Los miembros con acceso al repositorio o al servicio de firma deben utilizar autenticación multifactor. Configurar y comprobar los permisos del servicio de firma forma parte del trabajo pendiente; este documento no certifica que ese paso ya se haya realizado.

## Qué se firma

El único ejecutable propio de distribución es `ChilePrimeRepair.exe`. Se construye a partir del código público en un runner Windows alojado por GitHub, con la versión del SDK fijada en `global.json`. La configuración en `.signpath/artifact-configuration.xml` limita el nombre del archivo, el producto, la compañía y la versión. Los componentes .NET y Montserrat mantienen sus licencias y avisos originales.

La solicitud de firma solo se prepara mediante el workflow manual de la rama `main`, después de aprobar las pruebas del núcleo y del asistente con instalaciones simuladas. No se firman archivos externos aportados por terceros. La solicitud incluye el artefacto de GitHub y su procedencia verificable.

El workflow de firma está inactivo hasta que SignPath apruebe el proyecto y se configuren su cuenta, proyecto, política, aprobación manual y acceso limitado al repositorio. Un secreto de firma nunca se almacena en el código ni en los ZIP de distribución.

Tras obtener el archivo firmado, se comprueba una firma Authenticode válida de SignPath Foundation con sello de tiempo, se ejecuta la prueba del asistente sobre ese binario y se generan los hashes de distribución. Esta comprobación no certifica mejoras de FPS ni corrige causas internas del servidor.

## Crédito que se activará después de la aprobación

La acreditación siguiente se publicará como efectiva únicamente cuando el servicio esté aprobado y proporcione firmas de producción:

> Free code signing provided by SignPath.io, certificate by SignPath Foundation.

Actualmente describe el servicio solicitado; **no una firma disponible**. Las páginas de descarga y las notas de cada versión deben indicar su estado real: sin firma o firmada.

## Privacidad

El corrector no transmite información a sistemas de red. Los respaldos permanecen en el equipo del usuario. Véase [la política de privacidad](PRIVACY.md).
