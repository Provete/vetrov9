# Estação Vetrov-9 — Bíblia Técnica do Cenário

## 1. Histórico

**1962** — Uma expedição geofísica soviética de levantamento sísmico, operando no planalto interior da Antártida, registra um padrão de tremores quase-periódicos numa região sem falha geológica catalogada. A relação entre magnitude e duração dos eventos não bate com nenhum modelo conhecido de atividade tectônica ou ruptura de gelo.

**1963** — É erguida a Estação Vetrov-9, oficialmente o 9º nó de um braço soviético análogo à rede sismográfica mundial de monitoramento de testes nucleares (o tipo de rede que, na vida real, países usavam pra detectar detonações atômicas de longe através da assinatura sísmica). Essa é a fachada perfeita: instrumentação sísmica de ponta numa base isolada não levanta suspeita nenhuma. Internamente, porém, a estação carrega uma diretiva paralela: catalogar o "fenômeno acústico-sísmico anômalo, Setor 9" — origem do nome.

**1968 / 1979** — Dois períodos de atividade intensa ("flare-ups") são documentados. No segundo, o intervalo entre eventos sucessivos já é notavelmente menor que no primeiro — como se a cadência estivesse acelerando ao longo das décadas.

**1991** — Colapso da URSS. A estação quase é abandonada; um instituto sucessor assume a operação com equipe mínima rotativa.

**1998** — Novo flare-up, breve, coincide com uma modernização parcial dos dataloggers (parte do equipamento vira digital, parte continua analógica — daí a mistura "retrô" de protocolos que a estação carrega até hoje).

**2004** — A tripulação restante é retirada. Motivo oficial nos registros: "condições logísticas insustentáveis." A partir daqui a estação passa a operar 100% automatizada e não-tripulada, com telemetria retransmitida em janelas de uplink por satélite (não é um stream contínuo — os dados chegam em rajadas).

**2011 / 2016** — Dois flare-ups adicionais, monitorados inteiramente à distância.

**Hoje** — Um novo flare-up está em curso. O array completo foi reativado remotamente e a operadora contratante (privada, herdeira do acervo depois de trocar de mãos mais de uma vez) acaba de atribuir a supervisão do feed a um novo analista remoto: você.

## 2. Geografia

- Planalto interior antártico, ~1.400 km da costa mais próxima, altitude ~3.100 m.
- Temperatura média anual: -52 °C (mínimas de inverno abaixo de -80 °C).
- Espessura do gelo sob o Domo: ~2.600 m.
- Sob o gelo, um lago subglacial de água líquida mantida pela pressão e pelo calor geotérmico ("Lago Sredinnoye" nos mapas soviéticos), começando por volta dos 2.550 m de profundidade, com uma coluna d'água estimada de 80–150 m antes do embasamento rochoso.
- Ventos katabáticos (massas de ar frio que descem o platô por gravidade) são o principal fator climático e a maior fonte de ruído ambiental do array.

## 3. Topologia da rede de sensores

Coordenadas em metros, origem (0,0) no Domo central. Isso é o suficiente pra você calcular diferença de tempo de chegada (TDOA) entre nós.

| Nó | Posição (x, y) | Distância do Domo | Sensores presentes |
|---|---|---|---|
| DM-0 (Domo) | (0, 0) | — | Telemetria da estação, estação meteorológica |
| AA-1 (Anel A, N) | (0, 500) | 500 m | Sismômetro 3 eixos + microbarômetro |
| AA-2 (Anel A, L) | (500, 0) | 500 m | Sismômetro 3 eixos + microbarômetro |
| AA-3 (Anel A, S) | (0, -500) | 500 m | Sismômetro 3 eixos + microbarômetro |
| AA-4 (Anel A, O) | (-500, 0) | 500 m | Sismômetro 3 eixos + microbarômetro |
| AB-1 (Anel B, NE) | (1556, 1556) | 2.200 m | Sismômetro 3 eixos |
| AB-2 (Anel B, SE) | (1556, -1556) | 2.200 m | Sismômetro 3 eixos |
| AB-3 (Anel B, SO) | (-1556, -1556) | 2.200 m | Sismômetro 3 eixos |
| AB-4 (Anel B, NO) | (-1556, 1556) | 2.200 m | Sismômetro 3 eixos |
| BH-1 (poço profundo) | (50, -30) | ~58 m | String de termistores (até a interface gelo/lago) + hidrofone no fundo |
| BH-2 (poço raso) | (30, 40) | ~50 m | String de termistores (até 120 m, zona de firn) |
| MAG-1 | (0, 0) | co-localizado com o Domo | Magnetômetro fluxgate 3 eixos |
| MAG-2 | (1800, 0) | 1.800 m do MAG-1 | Magnetômetro fluxgate 3 eixos |

O Anel A (curto alcance, com infrassom) serve pra distinguir eventos de superfície de eventos profundos. O Anel B (longo alcance, só sísmico) serve pra triangular direção e distância com mais precisão geométrica.

## 4. Especificação dos sensores

| Sensor | Canais | Taxa de amostragem | Unidade | Banda / faixa útil | Comportamento de saturação |
|---|---|---|---|---|---|
| Sismômetro banda larga | Z, N, L (3 eixos) | 100 Hz | velocidade do solo (nm/s) | 0,05–40 Hz | satura acima de ~2 mm/s |
| Microbarômetro (infrassom) | 1 (pressão) | 20 Hz | pressão (mPa) | 0,01–8 Hz | satura com vento sustentado acima de ~15 m/s |
| Magnetômetro fluxgate | Z, N, L (3 eixos) | 1 Hz | campo magnético (nT) | DC–0,5 Hz | não satura, mas fica dominado por ruído em tempestade geomagnética |
| Termistor (string de poço) | 1 ponto a cada 100 m (BH-1) / 20 m (BH-2) | 1 amostra / 15 min | temperatura (°C) | -55 °C a 0 °C | ruído instrumental ±0,005 °C |
| Hidrofone (fundo do BH-1) | 1 | 2.000 Hz | pressão acústica (µPa) | 10 Hz–1 kHz | ambiente do lago é quase silêncio absoluto — qualquer sinal já é notável |
| Estação meteorológica | vento, temp. do ar, pressão | 1 amostra / min | m/s, °C, hPa | — | ventos katabáticos podem passar de 30 m/s |
| Telemetria da estação | RPM do gerador, consumo (kW), vibração do próprio equipamento | 1 Hz | rpm, kW, mm/s | — | usado como canal de referência pra filtrar ruído mecânico conhecido |

### Constantes físicas de propagação (para correlação/triangulação)

| Meio | Tipo de onda | Velocidade aproximada |
|---|---|---|
| Gelo sólido | Onda P (compressional) | ~3.800 m/s |
| Gelo sólido | Onda S (cisalhante) | ~1.900 m/s |
| Rocha (embasamento) | Onda P | ~5.500–6.000 m/s |
| Ar a -50 °C | Som / infrassom | ~300 m/s |
| Água do lago (próxima do congelamento) | Som | ~1.400 m/s |

## 5. Ruído de fundo "normal" (o que você precisa simular como baseline)

- **Criosismos**: rachaduras térmicas no gelo. Impulsivos, banda larga, duração menor que 1 s, dezenas por dia, mais frequentes à noite (contração térmica rápida). É a maior fonte de falso positivo sísmico.
- **Vento katabático**: eleva o piso de ruído do infrassom drasticamente e contamina levemente os sismômetros de superfície (vibração induzida em torres/equipamento). Correlaciona diretamente com os dados da estação meteorológica — o que permite ao sistema "explicar" picos sem precisar classificá-los como evento real.
- **Ruído do gerador**: harmônico fixo em 50/60 Hz + vibração mecânica constante, mais forte nos nós do Anel A por estarem mais perto do Domo. É previsível e serve como assinatura conhecida a ser filtrada usando o canal de telemetria como referência.
- **Variação geomagnética**: oscilação suave regular ao longo do dia (poucas dezenas de nT) e, ocasionalmente, tempestades geomagnéticas (clima espacial, sem relação nenhuma com a anomalia) que podem produzir variações de centenas de nT — outra fonte de falso positivo, dessa vez no magnetômetro.
- **Microssismos regionais reais**: terremotos distantes genuínos e de baixa magnitude. A assinatura clássica é a chegada da onda P seguida da onda S com um atraso proporcional à distância — um padrão bem comportado, bem diferente do padrão da anomalia.

## 6. A anomalia — "Traço Sigma"

O sistema automatizado nunca formalizou uma classificação pro fenômeno; internamente ele é só uma etiqueta de padrão de sinal: **Traço Sigma**. Não é um nome, é uma tag de correlação — o que por si só já diz algo sobre quanto a instituição sabe (ou admite saber).

**Assinatura sísmica**: sequência de eventos impulsivos com intervalo entre eventos de ~1,6–2,4 s (jitter irregular, lembrando uma cadência de passos), com decaimento de amplitude compatível com uma fonte pontual rasa — mas *dentro* do gelo ou da rocha, nunca na superfície.

**Ausência de infrassom correspondente**: esse é o cruzamento mais importante do cenário. Quando um evento do padrão Sigma acontece, o microbarômetro **não** registra nenhuma chegada correlata dentro da janela de trânsito esperada pra uma fonte de superfície. A ausência de sinal, aqui, é o sinal — é isso que indica que a fonte está abaixo do gelo, não em cima dele.

**Assinatura magnética**: uma perturbação dipolar pequena (poucas dezenas de nT), muito localizada e de queda rápida com a distância, que se desloca ao longo do tempo acompanhando a rota estimada pelos eventos sísmicos — como se algo com composição incomum (alto teor mineral ou metálico) estivesse de fato se movendo pelo subsolo.

**Assinatura térmica**: transientes breves e localizados nos termistores do poço mais próximo à rota estimada — um salto rápido de fração de grau acima da linha de base esperada, com retorno em minutos a horas. É bem diferente do gradiente geotérmico lento (~0,025 °C/m) que domina o resto da leitura.

**Assinatura acústica subaquática**: nos períodos de silêncio sísmico (sem eventos Sigma por várias horas), o hidrofone eventualmente capta vocalizações de frequência muito baixa (abaixo de 100 Hz) com estrutura harmônica peculiar. São raras — muito mais raras que os eventos sísmicos — e parecem ocorrer justamente quando "aquilo" não está se movendo.

**Padrão de longo prazo**: a atividade não é constante. Os flare-ups documentados desde 1962 mostram uma tendência (ainda que irregular) de os eventos ficarem mais frequentes e mais próximos do Domo a cada novo ciclo.

## 7. Como os sensores se cruzam (lógica de classificação, não implementação)

A força do cenário está em nenhum sensor sozinho provar nada — é a correlação entre eles que classifica um evento:

- **Sísmico + infrassom correlato dentro da janela esperada** → fonte de superfície (provável criosismo ou ruído de vento). Baixa prioridade.
- **Sísmico sem infrassom correlato** → fonte subsuperficial. Alta prioridade — candidato a Traço Sigma.
- **Sísmico com razão de velocidade P compatível com ~3.800 m/s entre nós** → provável trajeto no gelo.
- **Sísmico com razão de velocidade compatível com ~5.500 m/s** → provável trajeto na rocha, mais profundo, mais preocupante (mais perto do lago).
- **Pico magnético sem tempestade geomagnética simultânea (checar índice de atividade solar simulado)** → correlacionar posição estimada com a rota sísmica recente.
- **Transiente térmico num termistor específico** → cruzar com a proximidade daquele poço à rota estimada nas últimas horas.
- **Vocalização no hidrofone** → só é interessante quando não há atividade sísmica Sigma nas horas anteriores; junto com atividade sísmica, é só ruído biológico irrelevante (foco de biologia marinha sob o gelo, plausível mas sem relação).

## 8. Nota de sabor retrô (opcional)

Parte do equipamento nunca foi migrado dos anos 90. Um detalhe de textura, se for útil pra você: os dataloggers legados gravam os dados num formato binário fixo interno chamado **VDF (Vetrov Data Frame)** — cabeçalho com ID da estação, ID do canal, timestamp UTC em época Unix, contagem de amostras, o array de amostras, e um checksum simples de soma/XOR no final. Ninguém nunca teve orçamento pra substituir. É só uma sugestão de textura pra dar um ar de "formato de arquivo real e datado" caso você queira simular serialização/desserialização como parte dos testes — totalmente descartável se preferir passar direto pra structs em memória.
