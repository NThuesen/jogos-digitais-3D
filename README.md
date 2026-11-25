# Game Hub

Projeto Unity que junta **Ring Toss** (`../bako_slop1`) e **Pocket Putt** (`../Pocket-Put`) atrás de um menu inicial. Os projetos originais não foram alterados; este é uma cópia combinada.

## Como abrir

1. Abrir esta pasta com Unity **6000.5.9f1** (a versão do Ring Toss; o Pocket Putt estava em 6000.5.8f1).
2. Abrir `Assets/_Menu/Scenes/Menu.unity` e dar Play.

As cenas geradas já estão no projeto. Para recriá-las: **Game Hub > Build All Generated Scenes** (gera o campo do Pocket Putt, o menu e a lista de cenas do build).

## Estrutura

| Pasta | Conteúdo |
| --- | --- |
| `Assets/_Menu` | Menu inicial (`MainMenu`), navegação entre cenas (`HubNavigation`) e o gerador da cena (`Editor/HubSetup.cs`) |
| `Assets/_Game` | Ring Toss, no mesmo caminho do projeto original |
| `Assets/_PocketPutt` | Pocket Putt (era `Assets/_Game` no projeto original) |

Build Settings: `Menu` (índice 0), `Main` (Ring Toss), `C1-01_Prototype` (Pocket Putt).

## O que mudou nos jogos

- **Ring Toss:** botão **MENU** no canto superior esquerdo da tela de título. Durante a partida, o botão BACK leva ao título e de lá ao menu.
- **Pocket Putt:** botão **Menu** no HUD, ao lado de **Reiniciar**.
- Ajustes de integração, sem mudança de jogabilidade:
  - `Assets/_PocketPutt/Editor/*.cs`: caminhos apontam para `Assets/_PocketPutt`.
  - `GameSceneBootstrap.cs` (Ring Toss): ao regerar `Main.unity`, mantém as outras cenas do build em vez de substituir a lista.
  - `UnityAds.cs` (Ring Toss): não reinicializa o SDK de anúncios quando a cena do Ring Toss é aberta pela segunda vez na mesma sessão.
  - Identidade do app: `Jogos Digitais 3D` / `com.slipperyzlope.jogosdigitais3d`, para não sobrescrever o Ring Toss instalado no aparelho.

## Configurações compartilhadas

Um projeto Unity tem uma única configuração global. Prevaleceram as do Ring Toss, por ser o jogo mais acabado:

- **Pipeline URP `RingToss_URP`** para os dois jogos. O `MobileURP` do Pocket Putt usava o renderizador 2D, que não ilumina materiais 3D; com o renderizador 3D, a luz direcional que o gerador do Pocket Putt já cria passa a ter efeito.
- **Espaço de cor linear** (o Pocket Putt estava em gamma), então as cores do minigolfe podem parecer um pouco diferentes.
- **Retrato travado** nos dois jogos.
- **Passo de física:** o projeto usa 60 Hz (Ring Toss). `HubNavigation` aplica 50 Hz ao abrir o Pocket Putt, valor com que ele foi ajustado, e restaura 60 Hz ao voltar ao menu.
