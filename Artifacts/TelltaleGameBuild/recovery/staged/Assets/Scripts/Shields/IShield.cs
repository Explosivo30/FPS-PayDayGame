using UnityEngine;

public interface IShield
{
    float Current { get; }     // current shield value  
    float Max { get; }     // maximum shield capacity  
    void Absorb(float amount);  // absorbs incoming damage, returns overflow to health  
    void Regenerate();          // kick‑off regen if you want manual control  
}
