using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Item'ýn yere düþtükten sonra donmasýný saðlar
/// Masaya konulan item'lar için devre dýþý býrakýlmalý
/// </summary>
public class ItemFreezeSystem : NetworkBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[ItemFreezeSystem]";
    private const string GROUND_TAG = "Ground";
    private const float FREEZE_DELAY = 3f;

    #endregion

    #region Private Fields

    private Rigidbody rb;
    private bool isGrounded = false;
    private Coroutine freezeCoroutine;
    private bool isFrozenByTable = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError($"{LOG_PREFIX} Rigidbody bulunamadý!");
            return;
        }

        // Baþlangýçta constraint'leri kaldýr (masada deðilse)
        if (!isFrozenByTable)
        {
            rb.constraints = RigidbodyConstraints.None;
        }
    }

    private void OnEnable()
    {
        // Aktif edildiðinde reset
        isFrozenByTable = false;
        isGrounded = false;

        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }
    }

    private void OnDisable()
    {
        // Devre dýþý býrakýldýðýnda coroutine'i durdur
        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }
    }

    #endregion

    #region Collision Handling

    private void OnCollisionEnter(Collision collision)
    {
        // Masada ise collision'larý yoksay
        if (isFrozenByTable) return;

        // Sadece server'da çalýþ
        if (IsSpawned && !IsServer) return;

        if (collision.gameObject.CompareTag(GROUND_TAG))
        {
            if (!isGrounded)
            {
                isGrounded = true;

                if (freezeCoroutine != null)
                {
                    StopCoroutine(freezeCoroutine);
                }

                freezeCoroutine = StartCoroutine(FreezeAfterDelay(FREEZE_DELAY));
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (isFrozenByTable) return;

        if (IsSpawned && !IsServer) return;

        if (collision.gameObject.CompareTag(GROUND_TAG))
        {
            isGrounded = false;

            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
                freezeCoroutine = null;
            }
        }
    }

    #endregion

    #region Freeze Logic

    private IEnumerator FreezeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (rb != null && !isFrozenByTable)
        {
            rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            Debug.Log($"{LOG_PREFIX} {gameObject.name} freeze edildi!");
        }

        freezeCoroutine = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Freeze'i manuel olarak kaldýrýr
    /// </summary>
    public void UnfreezeItem()
    {
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.None;
        }

        isGrounded = false;
        isFrozenByTable = false;

        if (freezeCoroutine != null)
        {
            StopCoroutine(freezeCoroutine);
            freezeCoroutine = null;
        }

        Debug.Log($"{LOG_PREFIX} {gameObject.name} unfreeze edildi!");
    }

    /// <summary>
    /// Masaya konulduðunda çaðrýlýr - bu sistem devre dýþý kalýr
    /// </summary>
    public void SetTableFrozen(bool frozen)
    {
        isFrozenByTable = frozen;

        if (frozen)
        {
            // Coroutine'i durdur
            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
                freezeCoroutine = null;
            }

            isGrounded = false;
        }
    }

    #endregion
}