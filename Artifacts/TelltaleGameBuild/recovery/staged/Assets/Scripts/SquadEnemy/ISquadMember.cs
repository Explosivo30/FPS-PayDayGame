using UnityEngine;

public interface ISquadMember 
{

    /// <summary>
    /// El Transform del miembro (para leer su posici�n, rotaci�n, etc).
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// Este m�todo lo llamamos cada frame en AttackState para que el miembro
    /// persiga su posici�n asignada (offset) en formaci�n.
    /// </summary>
    /// <param name="formationPosition">Punto en world donde deber�a estar.</param>
    void MoveToFormationPosition(Vector3 formationPosition);
}
