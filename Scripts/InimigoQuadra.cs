using UnityEngine;

public class InimigoQuadra : MonoBehaviour
{
    private ArenaBasquete jogo;
    private Rigidbody2D corpo;
    private SpriteRenderer desenho;
    private float velocidade, tempo;
    private bool morto;
    public void Configurar(ArenaBasquete arena, float rapidez)
    {
        jogo = arena; velocidade = rapidez;
        desenho = GetComponent<SpriteRenderer>();
        // Inclui capacete, bracos, corpo e pes; a margem transparente fica de fora.
        Sprite sprite = desenho.sprite;
        var colisor = gameObject.AddComponent<BoxCollider2D>();
        colisor.size = new Vector2(120f, 156f) / sprite.pixelsPerUnit;
        colisor.offset = (new Vector2(64f, 78f) - sprite.pivot) / sprite.pixelsPerUnit;
        corpo = gameObject.AddComponent<Rigidbody2D>(); corpo.bodyType = RigidbodyType2D.Kinematic;
        corpo.useFullKinematicContacts = true;
    }
    private void FixedUpdate()
    {
        if (!jogo.EmJogo || morto) return;
        tempo += Time.fixedDeltaTime;
        desenho.sprite = jogo.AlienSprites[Mathf.FloorToInt(tempo * 7f) % jogo.AlienSprites.Length];
        desenho.flipX = jogo.Jogador.transform.position.x < transform.position.x;
        corpo.MovePosition(Vector2.MoveTowards(corpo.position, jogo.Jogador.transform.position, velocidade * Time.fixedDeltaTime));
    }
    public void Morrer()
    {
        if (morto) return;
        morto = true; jogo.AlienDerrotado(); gameObject.SetActive(false); Destroy(gameObject);
    }
    private void OnCollisionStay2D(Collision2D colisao)
    {
        if (!morto && colisao.gameObject.GetComponent<PlayerController2D>() != null) jogo.Machucar();
    }
}
