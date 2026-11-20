# Auditoria de entrega do Sertão · v2.8

Base do trabalho: `334efd0`, **Atualizações gerais v2.8**, do repositório `ArthurFuller/TESTE-GS2`. A auditoria foi reaplicada sobre a v2.8 com a correção das cartas. A regra dos Dias Tranquilos e o balanceamento de colheita vieram da v2.8.

## O que saiu

| Área | Mudanças |
| --- | --- |
| Raiz do projeto | Cópias de recuperação, versões duplicadas de cenas, três ZIPs de correções, a solução gerada do Visual Studio e relatórios exportados antigos. Os registros de design que ainda têm valor ficaram em `Docs/Historico`. |
| Modelos e texturas | Modelo de teste, duplicatas de modelos e mapas de textura sem referência, além de modelos legados de árvores, móveis e cerca quebrada que ficaram fora do jogo. As peças legadas que as cenas ou o Editor ainda leem continuam em `Assets/Modelos/ModelosLegados`. |
| Arte, áudio e vídeo | Exportações antigas do Figma, cursores antigos, arte de protótipo, a cópia duplicada do vídeo do estúdio, sprites antigos da loja e 17 clipes de áudio sem ligação com a biblioteca ou as cenas atuais. A animação antiga do tatu saiu porque o carregamento atual usa um flipbook. |
| Cena da partida | Painel de resumo diário substituído pelo fluxo atual de alimentação e escolha de carta, três linhas desativadas de compra de sementes, um grupo vazio de vegetação e o protótipo de teste de entrada em casa. Os marcadores usados pelos trabalhadores ficaram na cena. |
| Código | Métodos, propriedades e contadores sem consumidores, incluindo a rotina do resumo diário, o protótipo de entrada em casa, a penalidade de produção sem acionamento e os contadores antigos substituídos pelo registro da partida. |

O número de assets em `Assets` caiu de **947 para 769**. São **177 arquivos removidos** e um documento de créditos transferido para `Docs`. Outros **260 assets** foram movidos com seus `.meta`. O código próprio, incluindo as ferramentas do Editor, passou de **32.966 para 32.132 linhas**, uma redução de **834 linhas**.

## Segunda revisão de modelos e materiais

A revisão adicional retirou **100 arquivos** de `Assets` e os metadados correspondentes, reduzindo a pasta de **769 para 669 assets**. São **17 materiais, 73 imagens, quatro prefabs, dois modelos FBX, dois arquivos Unity `.asset`, um Shader Graph e um texto**. Entre os conjuntos retirados estão o cenário de teste da lagoa, uma casa decorativa e os prefabs antigos de pedras e árvores. A lista completa está em `Assets_Removidos_v2.8.md`.

Conferi os GUIDs referenciados pelas cenas, pelos prefabs, pelos materiais e pelos recursos carregados em tempo de execução. As 575 entradas alcançáveis pela varredura a partir das cenas e dos recursos ativos continuam no pacote. O modelo de terreno que a ferramenta de cenário carrega pelo caminho também foi mantido.

O log da importação anterior registrou 275 colisões de GUID com arquivos ainda presentes nos caminhos originais de `Assets`. A correção foi verificada extraindo este ZIP após retirar a pasta antiga. A checagem encontrou zero GUIDs e tipos C# duplicados na pasta nova. A compilação e os testes em build dependem da Unity.

## Como ficou a organização

- **Gameplay** em `Acoes`, `Economia`, `Eventos`, `Fazenda`, `Partida` e `Apresentacao`.
- **UI** em `Cartas`, `Componentes`, `InterfaceDaPartida`, `MenuERanking`, `MerceariaEEncomendas`, `Minigames` e `Tutorial`.
- **Editor** em configuração, cenário, interface e ferramentas de Play Mode. **Áudio** organizado por ambiente, cartas, coleta, eventos, fazenda e interface.
- **Cenas** com nomes claros para os Canvases e para os conjuntos principais. Na cena da partida, estoque e mercearia ficam em **Sistemas de gameplay**, e 530 objetos decorativos estão em grupos de gramas, cactos e árvores. A ferramenta de pintura continua encontrando o grupo usado no Editor.

## Melhorias no código

| Fluxo | Alteração |
| --- | --- |
| Preparo de comida | A mesma função calcula o rendimento real e o usado na previsão de encomendas. A simulação já conhece a quantidade de ingredientes e se são de dois tipos, então pode chamar o cálculo diretamente. |
| Mercearia | Cada linha atualiza os dados da oferta de uma vez. O reset de várias linhas atualiza os totais no fim. O saldo do estoque vem da mesma leitura usada pelas entregas. |
| Fazenda | Alterações de crescimento sem efeito deixam de disparar a atualização da mandala. Ela reaproveita as duas tabelas que compara e troca para a nova tabela quando há mudança. As condições e os valores dos bônus seguem o balanceamento da v2.8. |
| Editor e shader | Os seis campos de prévia dos ícones de encomendas compilam só no Editor. A máscara de terra arada recebeu valores iniciais explícitos e a base usada em `pow` ficou restrita a 0–1. A confirmação dos avisos D3D11 depende da compilação do shader na Unity. |

## Checagens

- **131 scripts próprios** analisados sintaticamente. Conferi as chamadas no C# e as referências serializadas antes de retirar rotinas e componentes. Os trechos de prévia foram conferidos com e sem o código exclusivo do Editor.
- **3.276 transforms mantidos** conferidos nas duas cenas. Comparei as poses, as relações entre pais e filhos, os IDs e as referências locais. Os **49 prefabs mantidos** estão idênticos à base.
- **11.520 comparações** entre o cálculo anterior de receitas e a chamada nova usada pela previsão de encomendas. O teste cobre tipos de ingrediente, limites personalizados, bônus e saturação do rendimento.
- As referências da build e os recursos carregados por código foram usados na revisão de assets. Os GUIDs dos arquivos movidos são os originais. Cada asset restante tem seus metadados correspondentes. `Packages`, `ProjectSettings` e `Firebase` são os mesmos do commit base.

- A correção das cartas passou por 80 cenários modelados de disposição e por simulações das duas viradas de dia com mais de 15 cartas. Os scripts das cartas foram conferidos após a mudança de pasta e a integração com a mercearia.

## Para fechar a entrega

1. Abra o projeto recém-extraído no **Unity 6000.3.8f1**. Confira o Console depois da importação.
2. Rode uma partida a partir de `MainMenu`. Passe pelo tutorial, Esc, preparo comum e Comida+, compra de cerca, tarefas dos trabalhadores, encomendas e fim da partida.
3. Gere e rode builds Windows e Web. Repare no shader de terra arada, na primeira abertura do ranking, na música e no funcionamento do nickname. As otimizações desta entrega ainda não foram medidas no Profiler.
4. Se a entrega for pública, confirme a autorização para redistribuir a fonte Xilosa, indicada como de uso pessoal. Confira os créditos da equipe e as permissões dos áudios de terceiros. [Página da Xilosa](https://www.dafont.com/pt/xilosa.font)

Os pontos com maior retorno para uma etapa posterior são medir a geração de encomendas e a primeira abertura do ranking, revisar dependências de Editor com uma importação limpa e reduzir o tamanho dos controladores grandes quando houver testes de jogo cobrindo seus fluxos. O ranking ainda lê a lista inteira. Os pacotes candidatos à revisão incluem Timeline, Visual Scripting, Multiplayer Center e Collaborate, que precisam ser checados no Editor antes de sair.
