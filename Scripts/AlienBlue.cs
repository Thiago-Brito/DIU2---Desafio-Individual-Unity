using UnityEngine;

public class AlienBlue : MonoBehaviour
{
    private PartidaAlien partida;
    private Transform alvo;
    private Sprite[] quadros;
    private SpriteRenderer desenho;
    private Rigidbody2D corpo;
    private float velocidade, tempo;
    private bool morto;
    public CircleCollider2D EsferaBranca { get; private set; }

    public void Configurar(PartidaAlien jogo, Transform jogador, Sprite[] imagens, float rapidez)
    {
        partida = jogo;
        alvo = jogador;
        quadros = imagens;
        velocidade = rapidez;
        desenho = GetComponent<SpriteRenderer>();
        corpo = GetComponent<Rigidbody2D>();
        // Centro e raio do capacete no PNG original (128 x 256).
        Sprite sprite = quadros[0];
        EsferaBranca = GetComponent<CircleCollider2D>();
        EsferaBranca.offset = (new Vector2(64f, 100f) - sprite.pivot) / sprite.pixelsPerUnit;
        EsferaBranca.radius = 53f / sprite.pixelsPerUnit;
        CircleCollider2D tronco = gameObject.AddComponent<CircleCollider2D>();
        tronco.isTrigger = true;
        tronco.offset = (new Vector2(64f, 35f) - sprite.pivot) / sprite.pixelsPerUnit;
        tronco.radius = 30f / sprite.pixelsPerUnit;
    }

    private void Update()
    {
        if (morto || alvo == null) return;
        tempo += Time.deltaTime;
        desenho.sprite = quadros[Mathf.FloorToInt(tempo * 8f) % quadros.Length];
        desenho.flipX = alvo.position.x < transform.position.x;
    }

    private void FixedUpdate()
    {
        if (morto || alvo == null) return;
        corpo.MovePosition(Vector2.MoveTowards(corpo.position, (Vector2)alvo.position + Vector2.up * 0.1f,
            velocidade * Time.fixedDeltaTime));
    }

    private void OnTriggerEnter2D(Collider2D outro)
    {
        if (morto || !gameObject.activeInHierarchy) return;
        PlayerController2D jogador = outro.GetComponent<PlayerController2D>();
        if (jogador == null) return;
        partida.Reiniciar();
    }

    public void Morrer()
    {
        if (morto) return;
        morto = true;
        partida.Pontuar();
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
