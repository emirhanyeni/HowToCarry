using UnityEngine;

public class HoldingMechanic: MonoBehaviour
{
   public static HoldingMechanic Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
    }
}