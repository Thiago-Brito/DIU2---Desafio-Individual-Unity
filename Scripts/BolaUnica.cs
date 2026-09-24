using UnityEngine;

// Uma instancia alterna entre mao, simulacao fisica e retorno. Nunca cria municao extra.
public class BolaUnica : MonoBehaviour
{
    public bool NaMao { get; private set; }
    public bool Retornando { get; private set; }
    public float SegundosParaVoltar => Mathf.Max(0f, 6f - tempoSolta);
    public int Quiques { get; private set; }
    private ArenaBasquete jogo;
    private Rigidbody2D corpo;
    private CircleCollider2D colisor;
    private SpriteRenderer desenho;
    private PhysicsMaterial2D material;
    private TrailRenderer trilha;
    private float tempoSolta, parado, ultimoQuique;
    private bool acertou, especial;

    public void Configurar(ArenaBasquete arena)
    {
        jogo = arena;
        desenho = GetComponent<SpriteRenderer>();
        corpo = gameObject.AddComponent<Rigidbody2D>();
        corpo.gravityScale = 1.65f;
        corpo.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        corpo.interpolation = RigidbodyInterpolation2D.Interpolate;
        corpo.mass = 0.6f;
        colisor = gameObject.AddComponent<CircleCollider2D>();
        colisor.radius = 0.15f;
        material = new PhysicsMaterial2D("Borracha da bola") { bounciness = 0.82f, friction = 0.18f };
        colisor.sharedMaterial = material;
        Physics2D.IgnoreCollision(colisor, jogo.Jogador.GetComponent<Collider2D>());
        trilha = gameObject.AddComponent<TrailRenderer>();
        trilha.sharedMaterial = jogo.MaterialEfeito;
        trilha.startWidth = 0.18f; trilha.endWidth = 0f;
        trilha.time = 0.22f; trilha.sortingOrder = 8;
        trilha.startColor = new Color(1f, 0.7f, 0.25f, 0.6f);
        trilha.endColor = Color.clear;
        Recolher(false);
    }

    public Vector3 Mao => jogo.Jogador.transform.position +
        new Vector3(jogo.Jogador.GetComponent<SpriteRenderer>().flipX ? -0.42f : 0.42f, 0.1f, 0f);

    public Vector2 DirecaoMira()
    {
        float lado = jogo.Jogador.GetComponent<SpriteRenderer>().flipX ? -1f : 1f;
        Vector2 direcao = new Vector2(lado, .3f);
        if (UnityEngine.InputSystem.Mouse.current != null && Camera.main != null)
        {
            Vector3 alvo = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            Vector2 ateMouse = (Vector2)(alvo - Mao);
            if (ateMouse.sqrMagnitude >= .01f) direcao = ateMouse;
        }
        return direcao.normalized;
    }

    public void Arremessar(Vector2 direcao, bool super)
    {
        if (!NaMao || !jogo.EmJogo) return;
        NaMao = false; Retornando = false; especial = super; acertou = false;
        jogo.Jogador.TemBola = false;
        tempoSolta = parado = 0f; Quiques = 0; ultimoQuique = -1f;
        corpo.simulated = true;
        transform.position = Mao;
        corpo.position = Mao;
        corpo.linearVelocity = direcao.normalized * (super ? 17f : 12f);
        corpo.angularVelocity = -Mathf.Sign(direcao.x) * 700f;
        // A bola do jogador permanece laranja; vermelho identifica ataques inimigos.
        desenho.color = super ? new Color(1f, 0.93f, 0.65f) : Color.white;
        trilha.startColor = super ? new Color(1f, 0.9f, 0.25f, 0.85f) : new Color(1f, 0.7f, 0.25f, 0.6f);
        trilha.Clear(); trilha.emitting = true;
        jogo.Som(0);
    }

    private void Update()
    {
        if (!jogo.EmJogo) return;
        if (NaMao)
        {
            Vector3 mao = Mao;
            if (jogo.Jogador.EstaNoChao && jogo.Jogador.EstaMovendo)
                mao.y += -0.2f + Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.32f;
            transform.position = mao;
            return;
        }
        if (Retornando)
        {
            transform.position = Vector3.MoveTowards(transform.position, Mao, 22f * Time.deltaTime);
            if (Vector2.Distance(transform.position, Mao) < 0.25f) Recolher(false);
            return;
        }
        tempoSolta += Time.deltaTime;
        parado = corpo.linearVelocity.sqrMagnitude < 0.25f ? parado + Time.deltaTime : 0f;
        if (tempoSolta > 0.45f && Vector2.Distance(transform.position, jogo.Jogador.transform.position) < 0.72f)
            Recolher(true);
        else if (tempoSolta >= 6f || parado >= 1.5f || transform.position.y < -6f ||
                 Mathf.Abs(transform.position.x) > 12f || transform.position.y > 9f)
            Voltar();
    }

    public void Voltar()
    {
        if (NaMao) return;
        Retornando = true;
        corpo.linearVelocity = Vector2.zero; corpo.angularVelocity = 0f;
        corpo.simulated = false;
        jogo.Avisar("Bola voltando!", 1f);
    }

    public void Recolher(bool manual)
    {
        NaMao = true; Retornando = false;
        corpo.linearVelocity = Vector2.zero; corpo.angularVelocity = 0f; corpo.simulated = false;
        transform.rotation = Quaternion.identity;
        transform.position = Mao;
        desenho.color = Color.white;
        trilha.emitting = false; trilha.Clear();
        jogo.Jogador.TemBola = true;
        if (manual) { jogo.RecompensarColeta(); jogo.Som(2); }
    }

    private void OnCollisionEnter2D(Collision2D colisao)
    {
        if (!jogo.EmJogo || NaMao || Retornando) return;
        InimigoQuadra reserva = colisao.gameObject.GetComponent<InimigoQuadra>();
        if (reserva != null)
        {
            if (colisao.relativeVelocity.magnitude > 2f) reserva.Morrer();
            return;
        }
        ChefeQuadra chefe = colisao.gameObject.GetComponent<ChefeQuadra>();
        if (chefe != null)
        {
            if (!acertou && colisao.relativeVelocity.magnitude > 2f && chefe.PodeReceberDano)
            {
                acertou = true;
                bool cabeca = colisao.collider == chefe.Cabeca;
                jogo.AcertarChefe((especial ? 3 : 1) + (Quiques > 0 ? 1 : 0), Quiques > 0, cabeca);
            }
        }
        else if (Time.time - ultimoQuique > 0.12f && colisao.relativeVelocity.magnitude > 2f)
        {
            Quiques++; ultimoQuique = Time.time;
            jogo.Som(1, 0.35f);
        }
    }

    private void OnDestroy() { if (material != null) Destroy(material); }
}
