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
            Debug.LogError("ItemFreezeSystem: Rigidbody bulunamadý!");
        }

        // Baþlangýçta constraint'leri kaldýr
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Ground taglý objeyle çarpýþma kontrolü
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (!isGrounded)
            {
                isGrounded = true;

                // Eðer önceden baþlatýlmýþ bir coroutine varsa iptal et
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
        // Ground'dan ayrýldýðýnda
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

    // Ýsteðe baðlý: Freeze'i manuel olarak kaldýrmak için
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