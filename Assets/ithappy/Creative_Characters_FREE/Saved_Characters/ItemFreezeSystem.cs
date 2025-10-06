using System.Collections;
using UnityEngine;

public class ItemFreezeSystem : MonoBehaviour
{
    private Rigidbody rb;
    private bool isGrounded = false;
    private Coroutine freezeCoroutine;

    void Start()
    {
        // Rigidbody componentini al
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("ItemFreezeSystem: Rigidbody bulunamad�!");
        }

        // Ba�lang��ta constraint'leri kald�r
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Ground tagl� objeyle �arp��ma kontrol�
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!isGrounded)
            {
                isGrounded = true;

                // E�er �nceden ba�lat�lm�� bir coroutine varsa iptal et
                if (freezeCoroutine != null)
                {
                    StopCoroutine(freezeCoroutine);
                }

                // 3 saniye sonra freeze et
                freezeCoroutine = StartCoroutine(FreezeAfterDelay(3f));
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // Ground'dan ayr�ld���nda
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = false;

            // Freeze coroutine'i iptal et
            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
                freezeCoroutine = null;
            }
        }
    }

    IEnumerator FreezeAfterDelay(float delay)
    {
        // 3 saniye bekle
        yield return new WaitForSeconds(delay);

        // Rigidbody'yi freeze et
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            Debug.Log($"{gameObject.name} freeze edildi!");
        }

        freezeCoroutine = null;
    }

    // �ste�e ba�l�: Freeze'i manuel olarak kald�rmak i�in
    public void UnfreezeItem()
    {
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
            isGrounded = false;

            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
                freezeCoroutine = null;
            }
        }
    }
}