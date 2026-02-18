Tailwind CSS - RazorIdentity
============================

El layout ya incluye ~/css/tailwind.css. Por defecto hay un CSS mínimo.

Para generar la versión completa de Tailwind (todas las clases) necesitas Node.js:

1. Abrir terminal en la raíz del proyecto (donde está package.json).
2. Ejecutar: npm install
3. Ejecutar: npm run build:css

Eso generará wwwroot/css/tailwind.css con Tailwind completo.

Para desarrollo con recarga al cambiar estilos:
   npm run watch:css

Las vistas que Tailwind escanea están en Styles/input.css (@source).
Puedes usar clases como: class="flex gap-4 p-4 bg-slate-100 rounded-lg"
