using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace RogPhoneSdkDemo
{
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance;

        [SerializeField] GameObject asteroid = null;
        [SerializeField] WaitForSeconds spawnDelay;
        [SerializeField] GameObject gameOverPanel = null;
        [SerializeField] Button restartButton = null;

        void Start()
        {
            Instance = this;
            spawnDelay = new WaitForSeconds(5f);
            StartCoroutine(SpawnRoutine());
            restartButton.onClick.AddListener(OnRestartButtonPressed);
        }

        private IEnumerator SpawnRoutine()
        {
            while (true)
            {
                Vector2 spawnPos = new Vector2(
                    Random.Range(-LevelBounds.MaxBounds.x, LevelBounds.MaxBounds.x),
                    Random.Range(-LevelBounds.MaxBounds.y, LevelBounds.MaxBounds.y)
                    );

                Instantiate(asteroid, spawnPos, Quaternion.identity);

                yield return spawnDelay;
            }
        }

        public void EndGame()
        {
            StopAllCoroutines();
            gameOverPanel.SetActive(true);
        }

        private void OnRestartButtonPressed()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}