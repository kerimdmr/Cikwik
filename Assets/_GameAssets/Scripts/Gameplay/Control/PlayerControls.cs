using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // Giriş sistemi ve fizik değişkenleri
    private PlayerControls playerControls;
    private Vector2 moveInput;
    private Rigidbody rb;
    
    // Durum değişkenleri
    private bool isGrounded;
    private bool isSprinting = false; // Sprint (Koşma) modunda mı? (Hareket + Ctrl)
    private bool isWalking = false;   // Yavaş yürüme modunda mı? (Q)
    private float defaultMoveSpeed;   // Başlangıç hareket hızını tutar
    
    [Header("Hareket Ayarları")]
    public float moveSpeed = 6f; // Varsayılan yürüme hızı
    public float jumpForce = 7f; // Zıplama kuvveti

    [Header("Hız Değişkenleri")]
    public float fastSpeed = 12f; // Sprint (Koşma) hızı (Artık bu Ctrl ile tetikleniyor)
    public float slowSpeed = 3f;  // Q tuşu ile yavaş yürüme hızı

    [Header("Dönüş Ayarları")]
    [Tooltip("Dönüş hızı artık anlık olduğu için bu değer kullanılmamaktadır.")]
    public float rotationSpeed = 5000f; 
    // Modelin görsel yönelim ofseti.
    public float modelRotationOffset = 0f; 
    
    [Header("Zıplama Ayarları")]
    public int maxJumps = 2; // İki kere zıplama hakkı
    private int currentJumps = 0; // Mevcut kullanılan zıplama sayısı

    [Header("Zemin Kontrolü")]
    public float raycastDistance = 0.6f; 
    public LayerMask whatIsGround;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        if (rb == null)
        {
            Debug.LogError("Rigidbody (3D) bileşeni bulunamadı! Lütfen ekleyin.");
            enabled = false;
            return;
        }

        defaultMoveSpeed = moveSpeed;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        playerControls = new PlayerControls();
        
        // --- Giriş Olaylarını Bağlama ---
        playerControls.Player.Move.performed += OnMovePerformed;
        playerControls.Player.Move.canceled += OnMoveCanceled;
        playerControls.Player.Jump.performed += OnJump;

        // Sprint ve SlowDown girişlerini bağlama
        try {
            playerControls.Player.Sprint.performed += OnSprintPerformed;
            playerControls.Player.Sprint.canceled += OnSprintCanceled;
        } catch (System.Exception ex) {
             Debug.LogError($"Input Bağlama Hatası (Sprint): PlayerControls'da 'Sprint' eylemini bulamadı. Input Asset dosyasını kontrol edin. Hata: {ex.Message}");
        }
        
        try {
            playerControls.Player.SlowDown.performed += OnSlowDownPerformed;
            playerControls.Player.SlowDown.canceled += OnSlowDownCanceled;
        } catch (System.Exception ex) {
             Debug.LogError($"Input Bağlama Hatası (SlowDown): PlayerControls'da 'SlowDown' eylemini bulamadı. Input Asset dosyasını kontrol edin. Hata: {ex.Message}");
        }
    }

    private void OnEnable()
    {
        playerControls.Enable();
    }

    private void OnDisable()
    {
        playerControls.Player.Move.performed -= OnMovePerformed;
        playerControls.Player.Move.canceled -= OnMoveCanceled;
        playerControls.Player.Jump.performed -= OnJump;

        try {
            playerControls.Player.Sprint.performed -= OnSprintPerformed;
            playerControls.Player.Sprint.canceled -= OnSprintCanceled;
        } catch {}

        try {
            playerControls.Player.SlowDown.performed -= OnSlowDownPerformed;
            playerControls.Player.SlowDown.canceled -= OnSlowDownCanceled;
        } catch {}
        
        playerControls.Disable();
    }
    
    // --- Giriş İşleme Metotları (Events) ---
    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }
    
    // Sprint (Hızlanma) İşlevi - HERHANGİ BİR hareket (WASD) varken etkinleşir.
    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        // moveInput.sqrMagnitude > 0.1f = WASD'den herhangi bir tuşa basılı mı?
        if (moveInput.sqrMagnitude > 0.1f)
        {
            isSprinting = true;
            isWalking = false; 
        }
        else
        {
             // Hareket yoksa hızlanma moduna geçme.
             isSprinting = false;
        }
    }
    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprinting = false;
    }

    private void OnSlowDownPerformed(InputAction.CallbackContext context)
    {
        isWalking = true;
        isSprinting = false; 
    }
    private void OnSlowDownCanceled(InputAction.CallbackContext context)
    {
        isWalking = false;
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (currentJumps < maxJumps)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            currentJumps++;
        }
    }

    // --- Fizik Tabanlı Hareket ve Dönüş Mantığı ---
    
    void Update()
    {
        isGrounded = CheckGrounded();
        
        if (isGrounded)
        {
            currentJumps = 0;
        }

        // Güvenlik: Eğer Ctrl basılıyken tüm hareket tuşları (WASD) bırakılırsa Sprint'i iptal et.
        // (moveInput.sqrMagnitude < 0.1f demek, hareket girdisi yok demektir.)
        if (isSprinting && moveInput.sqrMagnitude < 0.1f)
        {
            isSprinting = false;
        }
    }

    private void FixedUpdate()
    {
        // Hız Seçimi 
        float currentSpeed = defaultMoveSpeed;
        if (isSprinting) // Ctrl + Herhangi bir hareket tuşu aktif mi?
        {
            currentSpeed = fastSpeed;
        }
        else if (isWalking) // Q tuşu aktif mi?
        {
            currentSpeed = slowSpeed;
        }

        // Yön Vektörünü Hesapla (Y ekseni sıfır)
        Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        // 1. Karakteri Hareket Ettir 
        Vector3 targetVelocity = inputDirection * currentSpeed; 
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

        // 2. Karakteri Hareket Yönüne Çevir (ANLIK SERT DÖNÜŞ)
        if (inputDirection.sqrMagnitude > 0.1f) // Hareket girişi varsa
        {
            Quaternion targetRotation = Quaternion.LookRotation(inputDirection);
            targetRotation *= Quaternion.Euler(0, modelRotationOffset, 0); 
            transform.rotation = targetRotation;
        }
        
        // EK GÜVENLİK: X ve Z ekseni rotasyonlarını sıfırlar.
        transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
    }
    
    private bool CheckGrounded()
    {
        // Nesnenin merkezinden aşağı doğru ışın göndererek zemin kontrolü
        return Physics.Raycast(transform.position, Vector3.down, raycastDistance, whatIsGround);
    }
}