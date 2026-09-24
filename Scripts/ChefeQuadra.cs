using UnityEngine;

public class ChefeQuadra : MonoBehaviour
{
    public int Vida { get; private set; } = 40;
    public int Fase => Mathf.Clamp(1 + (40 - Vida) / 10, 1, 4);
    public bool PodeReceberDano => jogo.EmJogo && transicao <= 0f && Vida > 0;
    private ArenaBasquete jogo;
    private Rigidbody2D corpo;
    private SpriteRenderer desenho;
    private Vector2 origem;
    private float proximoAtaque, transicao, relogio;
    private float sentidoPatrulha = -1f;
    public bool EstaPatrulhando => Fase >= 3 && PodeReceberDano;
    private int sequencia;
    public string Intencao { get; private set; }
    public CircleCollider2D Cabeca { get; private set; }
    private readonly Color[] cores = { Color.white, new Color(1f, 0.8f, 0.35f), new Color(1f, 0.5f, 0.7f), new Color(0.7f, 0.45f, 1f) };

    public void Configurar(ArenaBasquete arena)
    {
        jogo = arena; desenho = GetComponent<SpriteRenderer>();
        corpo = gameObject.AddComponent<Rigidbody2D>();
        corpo.bodyType = RigidbodyType2D.Kinematic;
        corpo.useFullKinematicContacts = true;
        corpo.interpolation = RigidbodyInterpolation2D.Interpolate;
        // PNG 128x256: capacete em (64,100), corpo e pes abaixo dele.
        Sprite sprite = desenho.sprite;
        Cabeca = gameObject.AddComponent<CircleCollider2D>();
        Cabeca.offset = (new Vector2(64f, 100f) - sprite.pivot) / sprite.pixelsPerUnit;
        Cabeca.radius = 55f / sprite.pixelsPerUnit;
        var c = gameObject.AddComponent<BoxCollider2D>();
        c.size = new Vector2(116f, 72f) / sprite.pixelsPerUnit;
        c.offset = (new Vector2(64f, 36f) - sprite.pivot) / sprite.pixelsPerUnit;
        origem = transform.position;
        Reiniciar();
    }
    public void Reiniciar()
    {
        Vida = 40; transicao = 2f; proximoAtaque = 2.5f;
        relogio = 0f; sequencia = 0; sentidoPatrulha = -1f;
        transform.position = origem; corpo.position = origem;
        transform.localScale = Vector3.one * 1.8f;
        Intencao = "Prepare o arremesso"; desenho.color = cores[0];
    }
    public void ReceberDano(int dano)
    {
        if (!PodeReceberDano) return;
        int anterior = Fase;
        Vida = Mathf.Max(0, Vida - dano);
        if (Vida == 0) { jogo.Vencer(); return; }
        if (Fase != anterior)
        {
            transicao = 2f; proximoAtaque = Fase >= 3 ? 0.8f : 2.5f;
            jogo.LimparPerigos(); jogo.RecompensaFase();
            jogo.Avisar("ROUND " + Fase + " - " + ArenaBasquete.NomesFases[Fase - 1], 2f);
        }
    }
    private void FixedUpdate()
    {
        if (!jogo.EmJogo || Vida <= 0) return;
        relogio += Time.fixedDeltaTime;
        desenho.sprite = jogo.AlienSprites[Mathf.FloorToInt(relogio * (Fase >= 3 ? 9f : 6f)) % jogo.AlienSprites.Length];
        desenho.flipX = Fase >= 3 ? sentidoPatrulha < 0f : jogo.Jogador.transform.position.x < transform.position.x;
        desenho.color = transicao > 0f ? Color.Lerp(cores[Fase - 1], Color.white, Mathf.PingPong(relogio * 4f, 1f)) : cores[Fase - 1];
        if (transicao > 0f) { transicao -= Time.fixedDeltaTime; return; }
        Vector2 destino = origem + new Vector2(0f, Mathf.Sin(relogio * 1.7f) * 0.25f);
        float velocidade = 4f;
        if (Fase >= 3)
        {
            if (corpo.position.x <= -7.3f) sentidoPatrulha = 1f;
            if (corpo.position.x >= 7.3f) sentidoPatrulha = -1f;
            destino = new Vector2(sentidoPatrulha * 7.4f, origem.y);
            velocidade = Fase == 4 ? 2.8f : 2.1f;
        }
        corpo.MovePosition(Vector2.MoveTowards(corpo.position, destino, velocidade * Time.fixedDeltaTime));
        proximoAtaque -= Time.fixedDeltaTime;
        if (proximoAtaque > 0f) return;
        sequencia++;
        if (Fase == 1) Rajada();
        else if (Fase == 2) Chuva();
        else if (Fase == 3) Rajada();
        else { if (sequencia % 3 == 0) Chuva(); else Rajada(); }
        proximoAtaque = Fase == 4 ? 1.15f : Fase == 3 ? 1.55f : 2.4f;
    }
    private void Rajada()
    {
        jogo.Som(5, 0.22f);
        Intencao = Fase >= 3 ? "PATRULHA - use as plataformas e desvie das bolinhas!" : "DISPARO - pule ou desvie";
        float lado = jogo.Jogador.transform.position.x < corpo.position.x ? -1f : 1f;
        Vector2 inicio = corpo.position + new Vector2(lado * 1.2f, -0.35f);
        Vector2 mira = ((Vector2)jogo.Jogador.transform.position + Vector2.up * 0.1f - inicio).normalized;
        int quantidade = (Fase == 4 ? 3 : Fase == 3 ? 2 : 1) + jogo.ProjeteisExtras;
        for (int i = 0; i < quantidade; i++)
        {
            float angulo = (i - (quantidade - 1) * 0.5f) * 18f;
            Vector2 direcao = Quaternion.Euler(0f, 0f, angulo) * mira;
            jogo.CriarPerigo(inicio, direcao * (Fase == 4 ? 5.2f : Fase == 3 ? 4.6f : 4f), 0.7f);
        }
    }
    private void Chuva()
    {
        jogo.Som(5, 0.3f);
        Intencao = "CHUVA - saia das colunas marcadas";
        int metade = jogo.ProjeteisExtras > 0 ? 2 : 1;
        int quantidade = metade * 2 + 1;
        float espacamento = metade == 2 ? 1.7f : 2.2f;
        float x = Mathf.Clamp(jogo.Jogador.transform.position.x, -9f, 9f);
        // Sorteia entre os indices que cabem na quadra sem sobrepor colunas nas bordas.
        int primeiro = Mathf.Max(0, Mathf.CeilToInt(quantidade - 1 - (9f - x) / espacamento));
        int ultimo = Mathf.Min(quantidade - 1, Mathf.FloorToInt((x + 9f) / espacamento));
        int colunaAlvo = Random.Range(primeiro, ultimo + 1);
        for (int i = 0; i < quantidade; i++)
        {
            float coluna = x + (i - colunaAlvo) * espacamento;
            jogo.MarcarColuna(coluna);
            jogo.CriarPerigo(new Vector2(coluna, 4.2f), Vector2.down * 6f, 1f);
        }
    }
    private void OnCollisionStay2D(Collision2D c)
    {
        if (c.gameObject.GetComponent<PlayerController2D>() != null) jogo.Machucar();
    }
}
