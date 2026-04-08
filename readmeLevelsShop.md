# Guía de Creación de Mejoras (Tienda de Niveles)

Este documento explica cómo añadir nuevas mejoras al sistema de tienda de tu juego. El sistema se basa en **ScriptableObjects** para facilitar la creación de contenido sin necesidad de programar cada mejora individualmente.

## 1. Crear un nuevo Asset de Mejora
Para añadir una nueva mejora (ej: Daño de Pistola, Salto del Jugador, etc.):

1.  En la ventana **Project**, haz clic derecho en la carpeta de tu elección (recomendado: `Assets/Resources/Upgrades` o similar).
2.  Selecciona: **Create > Upgrades > StatUpgrade**.
3.  Ponle un nombre descriptivo al archivo (ej: `Upgrade_PistolDamage`).

## 2. Configurar la Mejora (Inspector)
Selecciona el asset creado y rellena sus campos:

### UI
*   **Display Name**: El nombre que aparecerá en el botón de la tienda.
*   **Icon**: El sprite que se mostrará.

### Target (Objetivo)
*   **Target**: Define si la mejora afecta al `Player` o a una `Weapon`.
*   **Gun Type ID**: (Solo si el Target es `Weapon`) Debe coincidir con el `GunTypeID` definido en el script de tu arma (ej: "Pistol", "Rifle").
*   **Weapon Stat** / **Player Stat**: El valor específico que quieres modificar.
*   **Upgrade Mode**: 
    *   `Additive`: Suma el valor directamente (ej: +5 de vida).
    *   `Percentual`: Aplica el valor como un porcentaje (el sistema del Player debe estar preparado para interpretarlo).

### Levels (Niveles)
Define cuántos niveles tiene la mejora y qué coste/valor tiene cada uno:
*   Añade elementos a la lista **Levels**. Cada elemento representa un nivel (Nivel 1, Nivel 2, etc.).
*   **Cost**: Dinero necesario para comprar este nivel.
*   **Value**: El valor que se aplicará (ej: si es Nivel 1 y pones 10, el daño pasará a ser el base + 10).

## 3. Registrar la Mejora en el Manager
Para que la mejora aparezca en la tienda, el `UpgradeManager` debe conocerla:

1.  Busca el objeto `UpgradeManager` en tu escena (normalmente en un objeto llamado `Managers` o `GameManager`).
2.  En la lista **Catalog**, añade el asset de mejora que acabas de crear.

## 4. UI de la Tienda
El `ShopManager` se encarga de poblar los botones. Actualmente:
*   Busca los botones definidos en su lista `Shops`.
*   Asigna cada mejora del `Catalog` a un botón por orden.

> [!IMPORTANT]
> Si añades más mejoras al catálogo de las que hay botones físicos en la UI de la tienda, las últimas no aparecerán. Asegúrate de duplicar o añadir más `ShopButtons` en el panel de UI si es necesario.

---
### Flujo de Código (Resumen)
1.  **ShopButton**: Detecta el clic y llama a `UpgradeManager.Instance.BuyUpgrade(miMejora)`.
2.  **UpgradeManager**: Verifica si tienes dinero (`CurrencyManager`), resta el coste, sube el nivel interno y llama a `ApplyUpgrade`.
3.  **ApplyUpgrade**: Busca al jugador o a las armas activas y aplica el nuevo valor estadístico.
