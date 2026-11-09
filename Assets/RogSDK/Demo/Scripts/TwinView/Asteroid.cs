using System.Collections;
using UnityEngine;

namespace RogPhoneSdkDemo
{

    public class Asteroid : MonoBehaviour
    {
        [Header("Movement & Rotation")]
        [SerializeField, Range(0f, 3f)] private float maxSpeed = 3f;
        [SerializeField, Range(0f, 90f)] private float maxTorque = 3f;

        [Header("Breakaway")]
        [SerializeField] private GameObject asteroidToSpawn = null;
        [SerializeField] private int asteroidSpawnCount = 0;

        [Header("Explosion")]
        [SerializeField] private GameObject explosionEffect = null;

        [Header("Spawning")]
        [SerializeField] private SpriteRenderer spriteRenderer = null;
        [SerializeField] private Collider2D asteroidCollider = null;

        private Vector2 velocity = Vector2.zero;
        private float torque = 0f;
        private WaitForSeconds blinkDelay = new WaitForSeconds(0.1f);
        private Color spawnColorA = new Color(1, 1, 1, .1f);
        private Color spawnColorB = new Color(1, 1, 1, .5f);

        void Awake()
        {
            asteroidCollider.enabled = false;
        }

        void Start()
        {
            velocity = new Vector2(Random.Range(-maxSpeed, maxSpeed), Random.Range(-maxSpeed, maxSpeed));
            torque = Random.Range(-maxTorque, maxTorque);

            StartCoroutine(InitializeAsteroid());
        }

        void Update()
        {
            transform.Translate(velocity * Time.deltaTime, Space.World);
            transform.Rotate(Vector3.forward * Time.deltaTime * torque);
        }

        IEnumerator InitializeAsteroid()
        {
            int blinks = 0;
            asteroidCollider.enabled = false;

            for (int i = 0; i < 10; i++)
            {
                spriteRenderer.color = (blinks % 2 == 0) ? spawnColorA : spawnColorB;
                yield return blinkDelay;
                blinks++;
            }

            asteroidCollider.enabled = true;
            spriteRenderer.color = Color.white;
        }

        /// <summary>
        /// Takes Damage from projectile
        /// </summary>
        public void TakeDamage()
        {
            for (int i = 0; i < asteroidSpawnCount; i++)
            {
                Vector2 offsetPost = (Vector2)this.transform.position + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f));
                Instantiate(asteroidToSpawn, offsetPost, Quaternion.identity);
            }

            Instantiate(explosionEffect, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}