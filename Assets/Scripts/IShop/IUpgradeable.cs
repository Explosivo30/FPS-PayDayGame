using UnityEngine;
/// Cualquier cosa que pueda mejorarse (arma, personaje…)
public interface IUpgradeable
{
    string Id { get; }                  // identificador único
    int Level { get; }                  // nivel actual
    int MaxLevel { get; }               // nivel máximo permisible
    int GetUpgradeCost();               // coste de la siguiente mejora
    void ApplyUpgrade();                // sube de nivel (aplica cambio de estadísticas)
}
