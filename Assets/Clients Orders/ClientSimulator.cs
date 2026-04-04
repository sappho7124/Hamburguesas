using UnityEngine;
using System.Collections.Generic;

public class ClientSimulator : MonoBehaviour
{
    [Header("Simulation Setup")]
    public List<SittingSpot> allSittingSpots;
    public TextAsset[] customerJSONFiles; 

    private string fallbackJson = @"
    {
      ""profileName"": ""Simulated Client"",
      ""idealTemp"": 65,
      ""minTotalItems"": 3,
      ""maxTotalItems"": 5,
      ""ingredients"":[
        { ""name"": ""Bottom Bun"", ""min"": 1, ""max"": 1, ""weight"": 0 },
        { ""name"": ""Top Bun"", ""min"": 1, ""max"": 1, ""weight"": 0 },
        { ""name"": ""Meat Patty"", ""min"": 1, ""max"": 2, ""weight"": 50 }
      ],
      ""groupedItems"":[],
      ""reactions"": {
        ""success"": ""Gracias!"",
        ""wrongOrder"": ""Esto no es lo que pedi, yo pedi {ORDER} y esto es {SERVED}."",
        ""foodRaw"": ""¡Esto esta crudo!"",
        ""foodCooked"": ""¡Esta perfecto!"",
        ""foodBurnt"": ""¡Esto esta quemado!"",
        ""foodOnFire"": ""¡AHHH FUEGO!"",
        ""customReactions"": []
      }
    }";

    [ContextMenu("Spawn Client at Random Empty Table")]
    public void SpawnRandomClient()
    {
        List<SittingSpot> emptySpots = new List<SittingSpot>();
        foreach (var spot in allSittingSpots)
        {
            if (!spot.isOccupied) emptySpots.Add(spot);
        }

        if (emptySpots.Count == 0) return;

        SittingSpot chosenSpot = emptySpots[Random.Range(0, emptySpots.Count)];

        string jsonToUse = fallbackJson;
        if (customerJSONFiles != null && customerJSONFiles.Length > 0)
        {
            jsonToUse = customerJSONFiles[Random.Range(0, customerJSONFiles.Length)].text;
        }

        CustomerProfile profile = JsonUtility.FromJson<CustomerProfile>(jsonToUse);

        chosenSpot.OccupySpot();
        OrderManager.Instance.GenerateOrderForTable(chosenSpot, profile);
    }
}