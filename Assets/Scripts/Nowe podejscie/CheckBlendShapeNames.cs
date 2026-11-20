using UnityEngine;

public class CheckBlendShapeNames : MonoBehaviour
{
    void Start()
    {
        SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("Nie znaleziono SkinnedMeshRenderer!");
            return;
        }

        Mesh mesh = smr.sharedMesh;
        Debug.Log($"--- LISTA BLENDSHAPES W MODELU: {smr.name} (Iloœæ: {mesh.blendShapeCount}) ---");

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string name = mesh.GetBlendShapeName(i);
            // Wypisuje: INDEKS - NAZWA
            Debug.Log($"[{i}] -> {name}");
        }

        Debug.Log("------------------------------------------------");
    }
}