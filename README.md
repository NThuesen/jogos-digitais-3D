# Jogos Digitais 3D

Entrega acadêmica que reúne dois jogos em um único projeto Unity. O menu inicial tem um cartão para cada jogo e permite alternar entre eles na mesma sessão.

| Jogo | Como jogar | Como voltar ao hub |
| --- | --- | --- |
| **Pocket Putt** | Minigolfe 3D com 50 fases em cinco campeonatos. Na área de jogo, clique ou toque, puxe na direção oposta à tacada e solte. O comprimento do arraste determina a força. Menos tacadas rendem mais estrelas, que ajudam a liberar as finais. | Volte das fases para o mapa e depois para a tela inicial do Pocket Putt. Use **Voltar ao hub**. No teclado, **Esc** volta uma tela a partir da partida ou dos campeonatos. |
| **Bako Slop 1 (Ring Toss)** | Jogo de arremesso de argolas. Clique ou toque na área de jogo, arraste para cima em direção aos pinos e solte para lançar. Mova o arraste para os lados para ajustar a mira. | Na partida, use **BACK** para retornar à tela de título e depois **MENU** para abrir o hub. |

## Abrir e executar

1. Instale o **Unity 6000.5.9f1** pelo Unity Hub. Para gerar um executável, tenha o módulo de suporte a Windows instalado.
2. Clone o projeto.

   ```sh
   git clone https://github.com/NThuesen/jogos-digitais-3D.git
   ```

3. No Unity Hub, use **Add project from disk** e selecione a pasta clonada. Na primeira abertura, o Unity baixa os pacotes declarados em `Packages/` pelos registros públicos Unity e OpenUPM.
4. Abra **`Assets/_Menu/Scenes/Menu.unity`**, selecione proporção **9:16** na aba **Game** e clique em **Play**.
5. Para gerar um executável, abra **File → Build Profiles**, selecione Windows e use a lista de cenas do projeto. `Menu` deve aparecer como primeira cena, seguida por `Main` (Bako) e `PocketPutt`. O executável abre o hub em uma janela retrato.

O Pocket Putt guarda seu progresso em `pocketputt_save.json` no diretório de dados do aplicativo. O Bako usa suas próprias chaves de `PlayerPrefs`. Os dois jogos mantêm o progresso ao voltar ao hub.

Na primeira entrada, o Bako pode mostrar sua tela de privacidade antes do título.

## Build WebGL para itch.io

Com o módulo WebGL do Unity instalado, execute `Unity -batchmode -nographics -projectPath <pasta-do-projeto> -buildTarget WebGL -executeMethod ItchWebBuild.Run -logFile <arquivo-de-log>`. A saída fica em `Builds/ItchWebGL`; compacte o conteúdo dessa pasta, com `index.html` na raiz do ZIP.

A página acompanha o tamanho do navegador no hub e no Ring Toss. Ao entrar no Pocket Putt, o canvas inteiro é centralizado em 9:16, com fundo neutro ao redor. Isso mantém UI Toolkit, câmera e coordenadas de entrada na mesma área; ao voltar ao hub, o canvas volta a ocupar a janela. O pós-processamento WebGL aplica o estilo responsivo na página gerada.

## Créditos

- Nicholas Thuesen
- Gabriel Ribeiro
- Alison Araujo
- Felipe Bakowski

O grupo montou o hub em conjunto. Pocket Putt é creditado a PocketSlop Games. Bako Slop 1 e seu vídeo de abertura são creditados a SlipperyZlope Games, conforme a identificação do jogo recebido. A publicação acadêmica do código e dos recursos foi autorizada pelos titulares.

O projeto usa Unity, URP, Input System, Cinemachine, Unity UI e TextMesh Pro. PrimeTween é de Kyrylo Kuzyk (MIT). O External Dependency Manager for Unity é distribuído sob Apache 2.0 e mantém sua licença na pasta do plugin. A fonte Liberation Sans mantém a licença OFL junto ao arquivo de fonte.

## Proveniência e limitações

- O hub e o Bako vieram do `GameHub.zip`. A versão antiga do Pocket Putt era o protótipo jogável de C1-01. O controlador coincide com o de `611fe8e8d53f0404e88cc91a9d8187e0923cb2ff` após descontar o botão do hub. O ZIP não contém histórico Git para confirmar o commit de origem de toda a cópia.
- O Pocket Putt integrado vem da branch `consolidate/repository-20261123`, commit `a369c3130363a02f515cbd676e9115e72d13b8ac`. O jogo usa o pipeline URP e o espaço de cor linear do projeto integrado.
- O AdMob foi omitido desta edição. A celebração e a progressão seguem com o provedor offline do Pocket Putt. O Bako mantém os fluxos locais de anúncio simulado, com os IDs de produção removidos.
- O teste de enquadramento retrato da fase **C1-09** é uma falha conhecida da versão consolidada. A fase e o critério de teste foram preservados.

## Validação da integração

- Unity Test Framework em 6000.5.9f1. Passaram 166 testes EditMode e 82 PlayMode, incluindo duas rodadas de alternância entre os jogos com verificação dos saves e do estado da cena.
- Todos os testes automatizados do Bako que passaram na base recebida também passaram na integração. Sete testes PlayMode de anúncios, captura e auditoria são executados sob demanda.
- Os 18 grupos de QA de rotas do Pocket Putt passaram. A build Windows x64 foi gerada a partir de uma cópia limpa, com zero erros e 13 avisos.
- A campanha automatizada em PlayMode concluiu as 50 fases com 150 estrelas e cinco troféus em um save isolado. O teste de enquadramento de C1-09 segue reprovado.
