using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(SpriteRenderer))]
public class PlayerController2D : MonoBehaviour
{
    public bool EstaNoChao => noChao;
    public bool EstaMovendo => Mathf.Abs(direcao) > 0.1f;
    public bool EstaAbaixado => abaixando;
    public bool EstaDeslizando => deslizandoAgora;

    [Header("Movimento")]
    [SerializeField] private float velocidade = 6f;
    [SerializeField] private float forcaDoPulo = 12f;
    [SerializeField] private float velocidadeDeslizando = 20f;

    [Header("Sprites")]
    [SerializeField] private Sprite parado;
    [SerializeField] private Sprite caminhada1;
    [SerializeField] private Sprite caminhada2;
    [SerializeField] private Sprite pulando;
    [SerializeField] private Sprite abaixado;
    [SerializeField] private Sprite deslizando;
    [SerializeField] private Sprite conduzindo1;
    [SerializeField] private Sprite conduzindo2;
    [SerializeField] private Sprite segurandoBola;

    private Rigidbody2D corpo;
    private CapsuleCollider2D colisor;
    private SpriteRenderer desenho;
    private Vector2 tamanhoEmPe;
    private Vector2 centroEmPe;
    private float direcao;
    private float tempoDaAnimacao;
    private bool noChao;
    private bool abaixando;
    private bool deslizandoAgora;
    private float fimDoDeslize;

    public void Reiniciar(Vector3 posicao)
    {
        transform.position = posicao;
        corpo.linearVelocity = Vector2.zero;
        corpo.angularVelocity = 0f;
        direcao = tempoDaAnimacao = fimDoDeslize = 0f;
        noChao = abaixando = deslizandoAgora = false;
        desenho.flipX = false;
        AtualizarColisor();
        AtualizarDesenho();
    }

    private void Awake()
    {
        corpo = GetComponent<Rigidbody2D>();
        colisor = GetComponent<CapsuleCollider2D>();
        desenho = GetComponent<SpriteRenderer>();
        tamanhoEmPe = colisor.size;
        centroEmPe = colisor.offset;
    }

    private void Update()
    {
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;

        direcao = 0f;
        if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) direcao = -1f;
        if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) direcao = 1f;

        abaixando = teclado.sKey.isPressed || teclado.downArrowKey.isPressed;

        if ((teclado.spaceKey.wasPressedThisFrame || teclado.wKey.wasPressedThisFrame ||
             teclado.upArrowKey.wasPressedThisFrame) && noChao && !abaixando)
        {
            corpo.linearVelocity = new Vector2(corpo.linearVelocity.x, forcaDoPulo);
        }

        if ((teclado.leftShiftKey.wasPressedThisFrame || teclado.rightShiftKey.wasPressedThisFrame) &&
            noChao && Mathf.Abs(direcao) > 0.1f)
        {
            deslizandoAgora = true;
            fimDoDeslize = Time.time + 0.45f;
        }

        if (Time.time >= fimDoDeslize || !noChao) deslizandoAgora = false;
        AtualizarColisor();
        AtualizarDesenho();
    }

    private void FixedUpdate()
    {
        float movimento = deslizandoAgora
            ? (desenho.flipX ? -velocidadeDeslizando : velocidadeDeslizando)
            : direcao * velocidade;
        corpo.linearVelocity = new Vector2(movimento, corpo.linearVelocity.y);

        noChao = corpo.Cast(Vector2.down, new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Default"),
            useTriggers = false
        }, new RaycastHit2D[4], 0.12f) > 0;
    }

    private void AtualizarColisor()
    {
        bool pequeno = abaixando || deslizandoAgora;
        colisor.size = pequeno ? new Vector2(tamanhoEmPe.x, tamanhoEmPe.y * 0.58f) : tamanhoEmPe;
        colisor.offset = pequeno ? centroEmPe + Vector2.down * (tamanhoEmPe.y * 0.21f) : centroEmPe;
    }

    private void AtualizarDesenho()
    {
        if (direcao < -0.1f) desenho.flipX = true;
        if (direcao > 0.1f) desenho.flipX = false;

        if (!noChao) desenho.sprite = pulando;
        else if (deslizandoAgora) desenho.sprite = deslizando;
        else if (abaixando) desenho.sprite = abaixado;
        else if (Mathf.Abs(direcao) > 0.1f)
        {
            tempoDaAnimacao += Time.deltaTime;
            // Com a bola, usa as poses de ação para parecer que o braço
            // acompanha cada quique. Se faltarem, volta para a caminhada normal.
            Sprite primeiro = conduzindo1 != null ? conduzindo1 : caminhada1;
            Sprite segundo = conduzindo2 != null ? conduzindo2 : caminhada2;
            desenho.sprite = Mathf.FloorToInt(tempoDaAnimacao * 7f) % 2 == 0 ? primeiro : segundo;
        }
        else
        {
            tempoDaAnimacao = 0f;
            desenho.sprite = segurandoBola != null ? segurandoBola : parado;
        }
    }
}
