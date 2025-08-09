using UnityEngine;
using System.Collections.Generic;

public class SnowflakeManager : MonoBehaviour
{
    public RectTransform snowflakeController;
    public GameObject snowflakePrefab;
    public int snowflakeCount = 15;

    private List<GameObject> snowflakePool;
    private List<Vector2> directions;
    private List<float> speeds;

    void Start()
    {
        // Inicializar el pool
        InitializePool();

        // Activar todos los copos iniciales
        foreach (GameObject snowflake in snowflakePool)
        {
            ResetSnowflake(snowflake);
        }
    }

    void Update()
    {
        // Mover cada copo
        for (int i = 0; i < snowflakePool.Count; i++)
        {
            GameObject snowflake = snowflakePool[i];
            MoveSnowflake(snowflake, i);
        }
    }

    void InitializePool()
    {
        snowflakePool = new List<GameObject>();
        directions = new List<Vector2>();
        speeds = new List<float>();

        // Crear el pool de copos
        for (int i = 0; i < snowflakeCount; i++)
        {
            GameObject snowflake = Instantiate(snowflakePrefab, snowflakeController);
            snowflakePool.Add(snowflake);
            directions.Add(Vector2.zero);
            speeds.Add(0f);
        }
    }

    void ResetSnowflake(GameObject snowflake)
    {
        float randomX = Random.Range(-snowflakeController.rect.width / 2, snowflakeController.rect.width / 2);
        snowflake.transform.localPosition = new Vector3(randomX, snowflakeController.rect.height / 2, 0);

        float randomSize = Random.Range(3f, 5f);
        RectTransform rectTransform = snowflake.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(randomSize, randomSize);

        float randomHorizontal = Random.Range(0.3f, 1f);
        int index = snowflakePool.IndexOf(snowflake);
        directions[index] = new Vector2(randomHorizontal, -1f).normalized;

        speeds[index] = Random.Range(50f, 150f);
    }

    void MoveSnowflake(GameObject snowflake, int index)
    {
        Vector3 movement = directions[index] * speeds[index] * Time.deltaTime;
        snowflake.transform.localPosition += movement;

        if (snowflake.transform.localPosition.y < -snowflakeController.rect.height / 2 ||
            snowflake.transform.localPosition.x > snowflakeController.rect.width / 2)
        {
            ResetSnowflake(snowflake);
        }
    }
}
