using UnityEngine;

public interface ISquadMember 
{

    /// <summary>
    /// El Transform del miembro (para leer su posición, rotación, etc).
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// Este método lo llamamos cada frame en AttackState para que el miembro
    /// persiga su posición asignada (offset) en formación.
    /// </summary>
    /// <param name="formationPosition">Punto en world donde debería estar.</param>
    void MoveToFormationPosition(Vector3 formationPosition);
}
