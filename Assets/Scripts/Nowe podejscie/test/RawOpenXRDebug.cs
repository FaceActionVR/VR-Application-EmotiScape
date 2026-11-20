using UnityEngine;
using System;
using System.Reflection;
using System.Linq;

public class RawOpenXRDebug : MonoBehaviour
{
    private MethodInfo getFacialExpressionsMethod;
    private object eyeTrackingTypeEnum;
    private Type htcExpressionEnum;

    // Bufor na dane (niezale¿ny od wersji SDK)
    private float[] weightsBuffer = new float[64];
    private bool initSuccess = false;

    void Start()
    {
        Debug.Log("--- SZUKANIE STEROWNIKA W PAMIÊCI (REFLECTION) ---");

        // 1. Szukamy klasy g³ównej (mo¿e byæ ViveFacialTracking lub FacialTracking)
        // Przeszukujemy wszystkie za³adowane biblioteki (Assemblies)
        Type driverClass = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == "ViveFacialTracking" || t.Name == "FacialTracking");

        if (driverClass == null)
        {
            Debug.LogError("B£¥D KRYTYCZNY: Nie znaleziono klasy sterownika VIVE w pamiêci! Czy plugin OpenXR jest w³¹czony?");
            return;
        }
        Debug.Log($"Znaleziono klasê sterownika: {driverClass.FullName}");

        // 2. Szukamy metody do pobierania danych
        getFacialExpressionsMethod = driverClass.GetMethods()
            .FirstOrDefault(m => m.Name == "GetFacialExpressions" && m.GetParameters().Length == 2);

        if (getFacialExpressionsMethod == null)
        {
            Debug.LogError("B£¥D: Klasa jest, ale nie ma metody GetFacialExpressions!");
            return;
        }

        // 3. Rozkminiamy argumenty (Enumy) dynamicznie
        ParameterInfo[] pars = getFacialExpressionsMethod.GetParameters();
        Type enumType = pars[0].ParameterType; // Typ pierwszego argumentu (np. FaceTrackingType)

        // Szukamy wartoœci odpowiadaj¹cej za OCZY (zazwyczaj nazywa siê "Eye" lub ma wartoœæ 1)
        try
        {
            eyeTrackingTypeEnum = Enum.Parse(enumType, "Eye");
        }
        catch
        {
            // Jak nie znajdzie po nazwie "Eye", bierzemy pierwsz¹ wartoœæ z brzegu (czêsto 0 lub 1 to oczy)
            var values = Enum.GetValues(enumType);
            if (values.Length > 0) eyeTrackingTypeEnum = values.GetValue(0);
        }

        // 4. Szukamy Enuma z indeksami (¿eby wiedzieæ który numer to WIDE)
        // Szukamy typu, który ma w nazwie "EyeExpression" albo "EyeShape"
        htcExpressionEnum = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => (t.Name.Contains("EyeExpression") || t.Name.Contains("EyeShape"))
                                 && t.IsEnum && t.Name.Contains("HTC"));

        if (htcExpressionEnum != null)
        {
            Debug.Log($"Znaleziono mapê indeksów: {htcExpressionEnum.Name}");
        }

        initSuccess = true;
        Debug.Log("--- INICJALIZACJA SUKCES. CZYTAM DANE... ---");
    }

    void Update()
    {
        if (!initSuccess) return;

        // Wywo³anie metody "na chama" przez Refleksjê
        // object[] args = { typ_trackingu, tablica_wynikowa };
        object[] args = new object[] { eyeTrackingTypeEnum, weightsBuffer };

        bool result = (bool)getFacialExpressionsMethod.Invoke(null, args);

        if (result)
        {
            // Odbieramy zmodyfikowan¹ tablicê
            float[] data = (float[])args[1];

            // W standardzie HTC OpenXR:
            // Indeks 1 = Left Wide
            // Indeks 3 = Right Wide
            // Indeks 4 = Left Squeeze

            float leftWide = data[1];
            float rightWide = data[3];
            float squeeze = data[4];

            if (leftWide > 0.001f || rightWide > 0.001f || squeeze > 0.01f)
            {
                // Wyœwietlamy czyste dane z sensora
                Debug.Log($"[HARDWARE RAW] L_WIDE: {leftWide:F3} | R_WIDE: {rightWide:F3} | SQUEEZE: {squeeze:F3}");
            }
        }
    }
}