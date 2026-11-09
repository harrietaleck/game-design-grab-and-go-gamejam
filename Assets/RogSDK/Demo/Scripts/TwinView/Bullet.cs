using UnityEngine;

namespace RogPhoneSdkDemo
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] float speed = 5f;

        void Start()
        {
            Destroy(gameObject, 2f);
        }

        // Update is called once per frame
        void Update()
        {
            transform.Translate(Vector2.up * speed * Time.deltaTime, Space.Self);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.CompareTag("Player"))
            {
                collision.gameObject.GetComponent<Asteroid>().TakeDamage();
                Destroy(gameObject);
            }
        }
    }
}