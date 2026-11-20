using Telemetry.Interactions;
using UnityEngine;

public class CauldronIngredientTrigger : MonoBehaviour
{
    public CauldronPotionRecipes recipes;

    [Header("Telemetry")]
    [SerializeField]
    private InteractionEventLogger interactionLogger;

    private void Awake()
    {
        ResolveTelemetry();
    }

    private void ResolveTelemetry()
    {
        if (interactionLogger == null)
        {
            interactionLogger =
                FindObjectOfType<InteractionEventLogger>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var ing = other.GetComponentInParent<IngredientPickup>();
        if (!ing || !ing.ingredient || ing.consumed) return;

        string objectName = ing.gameObject.name;
        ing.consumed = true;

        ResolveTelemetry();

        if (interactionLogger != null)
        {
            interactionLogger.LogPointEvent(
                "ITEM_ADDED_TO_CAULDRON",
                objectName,
                "{\"destination\":\"Cauldron\"}"
            );
        }
        recipes.AddIngredient(ing.ingredient);

        Destroy(ing.gameObject); // kopia znika, Ÿród³o zostaje
    }
}
