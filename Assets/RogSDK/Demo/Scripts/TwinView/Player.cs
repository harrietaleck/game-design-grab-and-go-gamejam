using UnityEngine;

namespace RogPhoneSdkDemo
{
    public class Player : MonoBehaviour
    {

        [Header("Movement")]
        [SerializeField] float acceleration = 1.2f;
        [SerializeField] float maxSpeed = 2f;
        [SerializeField] float deceleration = 5f;

        [Header("Attack")]
        [SerializeField] GameObject bullet = null;

        private Vector2 velocity = Vector2.zero;

        void Update()
        {
            Move();
        }

        #region Movement

        /// <summary>
        /// Sets the velocity.
        /// </summary>
        public void AddThrust()
        {
            velocity += (Vector2)transform.up * Time.deltaTime * acceleration;
            velocity.x = Mathf.Clamp(velocity.x, -maxSpeed, maxSpeed);
            velocity.y = Mathf.Clamp(velocity.y, -maxSpeed, maxSpeed);
        }

        /// <summary>
        /// Move and control decelerate
        /// </summary>
        private void Move()
        {
            transform.Translate(velocity, Space.World); // Move
            velocity = Vector2.MoveTowards(velocity, Vector2.zero, Time.deltaTime * deceleration); // decelerate
        }

        #endregion

        #region Attack

        /// <summary>
        /// Aim the specified direction
        /// </summary>
        public void Aim(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        public void Fire()
        {
            Instantiate(bullet, this.transform.position, this.transform.rotation);
        }

        #endregion

        #region Physics

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.CompareTag("Player"))
            {
                LevelManager.Instance.EndGame();
                Destroy(gameObject);
            }
        }

        #endregion
    }
}