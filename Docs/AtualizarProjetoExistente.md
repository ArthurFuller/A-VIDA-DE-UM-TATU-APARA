# Atualizar o projeto existente

Use o mesmo projeto que você já abre pelo Unity Hub.

1. Feche o Editor da Unity.
2. Extraia o ZIP na raiz do projeto, ao lado de `ProjectSettings` e `Packages`. A pasta nova se chama `Assets_PRONTA`. Se o Windows perguntar sobre o README, o .gitignore ou os arquivos de Docs, confirme a substituição.
3. Mova a pasta antiga `Assets` inteira para um backup fora do projeto. Na raiz do projeto, renomeie `Assets_PRONTA` para `Assets`.
4. Abra o mesmo projeto com Unity 6000.3.8f1. Aguarde a importação e confira o Console.

A pasta `Assets_PRONTA` contém todos os arquivos da auditoria v2.8 e os .meta com os GUIDs originais. A troca deve ser da pasta inteira. Colar o conteúdo por cima da `Assets` existente deixa scripts e recursos antigos junto dos movidos. Consulte `Auditoria_v2.8.md` e `Assets_Removidos_v2.8.md` para ver a limpeza.

## Arquivos antigos da raiz

Para concluir a limpeza da raiz do repositório, mova também estes itens antigos para o backup, caso existam:

- `.recovery`
- `Archive`
- `output`
- `ReversaoVisual_2026-11-13`
- `GS2_ALTERACOES.txt`
- `Sertao_Hover_e_Hitbox_Finalizar_Dia.zip`
- `Sertao_Respingos_no_Solo.zip`
- `TESTE-GS2_v2.7_Eventos_Dias_Tranquilos.zip`
- `TESTE-GS2.slnx` (arquivo gerado pelo editor)
