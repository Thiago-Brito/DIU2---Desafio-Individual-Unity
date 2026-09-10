using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PartidaAlien : MonoBehaviour
{
    [SerializeField] private PlayerController2D jogador;
    [SerializeField] private Sprite[] alienQuadros;
    [SerializeField] private Sprite[] bolaQuadros;
    private readonly List<AlienBlue> inimigos = new List<AlienBlue>();
    private readonly List<BolaProjetil> bolas = new List<BolaProjetil>();
    private Vector3 inicio;
    private Vector3 cameraInicial;
    private float tempo, proximoInimigo, proximoTiro;
    private int pontos;
    public int Nivel => 1 + Mathf.FloorToInt(tempo / 15f);

    private void Start()
    {
        inicio = jogador.transform.position;
        if (Camera.main != null) cameraInicial = Camera.main.transform.position;
        proximoInimigo = 2f;
    }

    private void Update()
    {
        if (jogador == null) return;
        tempo += Time.deltaTime;
        if (jogador.transform.position.y < -8f)
        {
            Reiniciar();
            return;
        }
        Keyboard teclado = Keyboard.current;
        bool atirando = (teclado != null && (teclado.jKey.isPressed || teclado.ctrlKey.isPressed)) ||
                        (Mouse.current != null && Mouse.current.leftButton.isPressed);
        if (atirando && tempo >= proximoTiro) Atirar();
        if (tempo >= proximoInimigo)
        {
            inimigos.RemoveAll(inimigo => inimigo == null);
            if (inimigos.Count < Mathf.Min(3 + Nivel, 15)) CriarInimigo();
            proximoInimigo = tempo + Mathf.Max(0.65f, 3.5f - (Nivel - 1) * 0.3f);
        }
        bolas.RemoveAll(bola => bola == null);
    }

    private void CriarInimigo()
    {
        GameObject objeto = new GameObject("alienBlue");
        objeto.transform.SetParent(transform);
        float lado = Random.value < 0.5f ? -1f : 1f;
        objeto.transform.position = jogador.transform.position + new Vector3(lado * 9f, Random.Range(0.5f, 2f), 0f);
        SpriteRenderer desenho = objeto.AddComponent<SpriteRenderer>();
        desenho.sprite = alienQuadros[0];
        desenho.sortingOrder = 5;
        // O alien flutua para conseguir perseguir o jogador nas plataformas.
        CircleCollider2D colisor = objeto.AddComponent<CircleCollider2D>();
        colisor.radius = 0.34f;
        colisor.isTrigger = true;
        Rigidbody2D corpo = objeto.AddComponent<Rigidbody2D>();
        corpo.bodyType = RigidbodyType2D.Kinematic;
        AlienBlue alien = objeto.AddComponent<AlienBlue>();
        alien.Configurar(this, jogador.transform, alienQuadros, Mathf.Min(5.2f, 1.25f + (Nivel - 1) * 0.25f));
        inimigos.Add(alien);
    }

    private void Atirar()
    {
        proximoTiro = tempo + 0.28f;
        float lado = jogador.GetComponent<SpriteRenderer>().flipX ? -1f : 1f;
        GameObject objeto = new GameObject("Bola arremessada");
        objeto.transform.SetParent(transform);
        objeto.transform.position = jogador.transform.position + new Vector3(lado * 0.4f, 0.08f, 0f);
        SpriteRenderer desenho = objeto.AddComponent<SpriteRenderer>();
        desenho.sprite = bolaQuadros[0];
        desenho.sortingOrder = 6;
        BolaProjetil bola = objeto.AddComponent<BolaProjetil>();
        bola.Configurar(jogador.GetComponent<Collider2D>(), bolaQuadros, lado);
        bolas.Add(bola);
    }

    public void Pontuar() => pontos++;

    public void Reiniciar()
    {
        foreach (AlienBlue alien in inimigos)
            if (alien != null) { alien.gameObject.SetActive(false); Destroy(alien.gameObject); }
        foreach (BolaProjetil bola in bolas)
            if (bola != null) { bola.gameObject.SetActive(false); Destroy(bola.gameObject); }
        inimigos.Clear();
        bolas.Clear();
        tempo = proximoTiro = 0f;
        proximoInimigo = 2f;
        pontos = 0;
        jogador.Reiniciar(inicio);
        if (Camera.main != null) Camera.main.transform.position = cameraInicial;
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(12, 12, 470, 76), "Alien Blue | Pontos: " + pontos + " | Nivel: " + Nivel);
        GUI.Label(new Rect(24, 38, 450, 22), "A/D ou setas: andar | Espaco: pular | S: abaixar");
        GUI.Label(new Rect(24, 59, 450, 22), "Shift: deslizar | J / Ctrl / clique esquerdo: atirar bola");
    }
}
