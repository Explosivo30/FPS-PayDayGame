# Adding another language

EZ Scene Collab exposes an extensible localization registry. A translation can be supplied from an Editor script in this package or from a separate integration.

1. Create a dictionary whose keys use the public constants in `EZCollabText`.
2. During Editor initialization, call `EZCollabLocalization.RegisterLanguage` with a short language code, display name, and dictionary.
3. The new language appears automatically in the EZ Scene Collab language selector.

Example:

```csharp
using System.Collections.Generic;
using UnityEditor;

namespace EZCollabTool.ExampleLocalization
{
    [InitializeOnLoad]
    public static class FrenchLanguage
    {
        static FrenchLanguage()
        {
            EZCollabLocalization.RegisterLanguage(
                "fr",
                "Français",
                new Dictionary<string, string>
                {
                    { EZCollabText.Language, "Langue" },
                    { EZCollabText.YourName, "Votre nom" },
                    { EZCollabText.StartHost, "Démarrer comme hôte" }
                });
        }
    }
}
```

Missing keys fall back to the English translation, so a new language can be added incrementally. Pass `replaceExisting: true` only when intentionally replacing a previously registered language with the same code.
