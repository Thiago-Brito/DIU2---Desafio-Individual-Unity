using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(SpriteRenderer))]
public class PlayerController2D : MonoBehaviour
{
    public bool EstaNoChao => noChao;
    public bool EstaMovendo => Mathf.Abs(direcao) > 0.1f;
    public bool EstaAbaixado => abaixando;
    public bool EstaDeslizando => deslizandoAgora;
    public bool TemBola { get; set; } = true;
    public bool ControleBloqueado { get; set; }
    private readonly RaycastHit2D[] contatosChao = new RaycastHit2D[8];
    private readonly RaycastHit2D[] contatosParede = new RaycastHit2D[8];
    private PhysicsMaterial2D semAtrito;
    private int ladoParede;
    private float vertical, saltoParedeAte, impulsoParede;
    public bool EstaEscalando { get; private set; }

    public void ConfigurarSprites(Sprite[] poses)
    {
        parado = poses[0]; caminhada1 = poses[1]; caminhada2 = poses[2];
        pulando = poses[3]; abaixado = poses[4]; deslizando = poses[5];
        conduzindo1 = poses[6]; conduzindo2 = poses[7]; segurandoBola = poses[8];
    }

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
    private bool puloVariavel;
    private int teclaDoPulo;
    private readonly System.Collections.Generic.List<Collider2D> plataformasIgnoradas = new System.Collections.Generic.List<Collider2D>();

    public void Reiniciar(Vector3 posicao)
    {
        RestaurarPlataformas(true);
        puloVariavel = false;
        transform.position = posicao;
        corpo.linearVelocity = Vector2.zero;
        corpo.angularVelocity = 0f;
        direcao = tempoDaAnimacao = fimDoDeslize = 0f;
        noChao = abaixando = deslizandoAgora = false;
        EstaEscalando = false; ladoParede = 0; vertical = saltoParedeAte = impulsoParede = 0f;
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
        semAtrito = new PhysicsMaterial2D("Jogador sem aderencia nos cantos") { friction = 0f, bounciness = 0f };
        colisor.sharedMaterial = semAtrito;
    }

    private void Update()
    {
        if (ControleBloqueado || Time.timeScale == 0f) { direcao = 0f; return; }
        Keyboard teclado = Keyboard.current;
        if (teclado == null) return;

        direcao = 0f;
        if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) direcao = -1f;
        if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) direcao = 1f;

        abaixando = teclado.sKey.isPressed || teclado.downArrowKey.isPressed;
        vertical = teclado.wKey.isPressed || teclado.upArrowKey.isPressed ? 1f : abaixando ? -1f : 0f;

        if (teclado.spaceKey.wasPressedThisFrame && abaixando && DescerPlataforma()) { }
        else if (teclado.spaceKey.wasPressedThisFrame && ladoParede != 0)
            SaltarDaParede();
        else if ((teclado.spaceKey.wasPressedThisFrame || teclado.wKey.wasPressedThisFrame ||
             teclado.upArrowKey.wasPressedThisFrame) && noChao && !abaixando && !EstaEscalando)
        {
            corpo.linearVelocity = new Vector2(corpo.linearVelocity.x, forcaDoPulo);
            puloVariavel = true;
            teclaDoPulo = teclado.spaceKey.wasPressedThisFrame ? 0 : teclado.wKey.wasPressedThisFrame ? 1 : 2;
        }

        bool segurandoPulo = teclaDoPulo == 0 ? teclado.spaceKey.isPressed :
            teclaDoPulo == 1 ? teclado.wKey.isPressed : teclado.upArrowKey.isPressed;
        if (puloVariavel && !segurandoPulo)
        {
            if (corpo.linearVelocity.y > 0f)
                corpo.linearVelocity = new Vector2(corpo.linearVelocity.x, corpo.linearVelocity.y * .4f);
            puloVariavel = false;
        }
        if (corpo.linearVelocity.y <= 0f) puloVariavel = false;

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
        RestaurarPlataformas(false);
        if (ControleBloqueado) { corpo.linearVelocity = Vector2.zero; return; }
        ladoParede = EncontrarParede();
        EstaEscalando = ladoParede != 0 && direcao * ladoParede > 0.1f && Time.time >= saltoParedeAte;
        if (EstaEscalando)
        {
            deslizandoAgora = false; abaixando = false;
            // Compensa a gravidade deste passo sem alterar a configuracao do Rigidbody.
            float subida = transform.position.y >= 3.4f && vertical > 0f ? 0f : vertical * 4f;
            corpo.linearVelocity = new Vector2(ladoParede * .2f, subida - Physics2D.gravity.y * corpo.gravityScale * Time.fixedDeltaTime);
            noChao = false;
            return;
        }
        float movimento = deslizandoAgora
            ? (desenho.flipX ? -velocidadeDeslizando : velocidadeDeslizando)
            : direcao * velocidade;
        if (Time.time < saltoParedeAte) movimento = impulsoParede;
        corpo.linearVelocity = new Vector2(movimento, corpo.linearVelocity.y);

        int total = corpo.Cast(Vector2.down, new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Default"),
            useTriggers = false
        }, contatosChao, 0.12f);
        noChao = false;
        for (int i = 0; i < total; i++)
            if (contatosChao[i].normal.y > 0.5f && !plataformasIgnoradas.Contains(contatosChao[i].collider) &&
                corpo.linearVelocity.y <= .1f) noChao = true;
    }

    public bool DescerPlataforma()
    {
        if (ControleBloqueado || Time.timeScale == 0f || !noChao) return false;
        int total = colisor.Cast(Vector2.down, new ContactFilter2D { useTriggers = false,
            useLayerMask = true, layerMask = LayerMask.GetMask("Default") }, contatosChao, .15f);
        bool encontrou = false;
        for (int i = 0; i < total; i++)
        {
            var hit = contatosChao[i];
            var plataforma = hit.collider.GetComponent<PlatformEffector2D>();
            if (hit.normal.y < .5f || !hit.collider.usedByEffector || plataforma == null || !plataforma.useOneWay) continue;
            if (!plataformasIgnoradas.Contains(hit.collider))
            {
                plataformasIgnoradas.Add(hit.collider);
                Physics2D.IgnoreCollision(colisor, hit.collider, true);
            }
            encontrou = true;
        }
        if (!encontrou) return false;
        puloVariavel = noChao = EstaEscalando = deslizandoAgora = false;
        saltoParedeAte = Time.time + .2f; impulsoParede = direcao * velocidade;
        corpo.linearVelocity = new Vector2(corpo.linearVelocity.x, -3f);
        return true;
    }

    private void RestaurarPlataformas(bool todas)
    {
        for (int i = plataformasIgnoradas.Count - 1; i >= 0; i--)
        {
            var plataforma = plataformasIgnoradas[i];
            // Usa a altura em pe para nao reativar a colisao ao soltar S no meio da passagem.
            float topo = transform.position.y + centroEmPe.y + tamanhoEmPe.y * .5f;
            if (!todas && plataforma != null && topo >= plataforma.bounds.min.y - .05f) continue;
            if (plataforma != null && colisor != null) Physics2D.IgnoreCollision(colisor, plataforma, false);
            plataformasIgnoradas.RemoveAt(i);
        }
    }

    private void OnDisable() { RestaurarPlataformas(true); puloVariavel = false; }

    private int EncontrarParede()
    {
        var filtro = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = LayerMask.GetMask("Default") };
        for (int lado = -1; lado <= 1; lado += 2)
        {
            int total = colisor.Cast(Vector2.right * lado, filtro, contatosParede, .09f);
            for (int i = 0; i < total; i++)
            {
                var hit = contatosParede[i];
                // Somente paredes estaticas altas; nunca aliens ou bordas das plataformas.
                if (hit.collider.attachedRigidbody == null && hit.collider.bounds.size.y > 2f &&
                    !hit.collider.usedByEffector && hit.normal.x * lado < -.6f) return lado;
            }
        }
        return 0;
    }

    public bool SaltarDaParede()
    {
        if (ControleBloqueado || Time.timeScale == 0f) return false;
        ladoParede = EncontrarParede();
        if (ladoParede == 0) return false;
        impulsoParede = -ladoParede * 8f; saltoParedeAte = Time.time + .25f;
        corpo.linearVelocity = new Vector2(impulsoParede, forcaDoPulo);
        puloVariavel = true; teclaDoPulo = 0;
        EstaEscalando = noChao = deslizandoAgora = false;
        return true;
    }

    private void OnDestroy() { if (semAtrito != null) Destroy(semAtrito); }

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
            Sprite primeiro = TemBola && conduzindo1 != null ? conduzindo1 : caminhada1;
            Sprite segundo = TemBola && conduzindo2 != null ? conduzindo2 : caminhada2;
            desenho.sprite = Mathf.FloorToInt(tempoDaAnimacao * 7f) % 2 == 0 ? primeiro : segundo;
        }
        else
        {
            tempoDaAnimacao = 0f;
            desenho.sprite = TemBola && segurandoBola != null ? segurandoBola : parado;
        }
    }
}
