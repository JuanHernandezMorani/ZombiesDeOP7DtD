# LiteFarming Module

LiteFarming añade una cadena de cultivo ligera compatible con War3zuk AIO. El módulo incluye:

- Plantas personalizadas de trébol, raíz exótica y algas subterráneas.
- Estaciones de cocina dedicadas (fermentador, ahumadero y olla de cocción lenta).
- Recetas temáticas que utilizan los nuevos ingredientes sin editar las recetas alteradas por War3zuk.

## Crecimiento de plantas
Las plantas nuevas siguen las reglas normales del juego: necesitan luz ambiental adecuada, agua cercana y el tiempo estándar de crecimiento. Estas comprobaciones se aplican mediante `FarmingManager.cs` sin reemplazar bloques ni plantas existentes.

## Límites de compatibilidad
- Ningún recurso original de War3zuk se modifica; todas las identificaciones comienzan con `LiteFarming`.
- Las estaciones avanzadas usan áreas de crafteo exclusivas (`liteFermenter`, `liteSmoker`, `liteSlowCooker`) para evitar conflictos.
- Los prefabs incluidos son marcadores de posición y deben sustituirse por prefabs exportados desde Unity antes de publicar el mod.
- El módulo depende de Harmony y del sistema de eventos vanilla para mantener la compatibilidad con otros mods.
