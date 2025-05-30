    using cAlgo.API;
    using cAlgo.API.Indicators;
    using System;
    using System.Collections.Generic;
    using System.Linq; // <— necessário para Concat()
    
    namespace cAlgo.Robots
    {
        [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
        public class AcceleratorTradingBot : Robot
        {
            private ExponentialMovingAverage ema;
            private AcceleratorOscillator ac;
            private Dictionary<string, int> contadorLossPorAtivo = new Dictionary<string, int>();
            private Dictionary<string, double> prejuizoAcumuladoPorAtivo = new Dictionary<string, double>();
            private Dictionary<string, long> ultimaOperacaoIdPorAtivo = new Dictionary<string, long>();
            private Dictionary<string, int> reentradasPorAtivo = new Dictionary<string, int>();
            private Dictionary<string, int> entradasPositivas = new Dictionary<string, int>();
    
            // Ativos para operar “contra” a USDX (quando o USDX cai, compramos estes ativos)
                private readonly string[] ativos =
                {
                    "EURUSD",  // par se beneficia de dólar mais fraco
                    "GBPUSD",
                    "AUDUSD",
                    "NZDUSD",
                    "EURGBP",  // também contra o dólar
                    "EURJPY",
                    "XAUUSD"   // ouro costuma subir com dólar fraco
                };
                
                // Ativos para operar “a favor” da USDX (quando o USDX sobe, compramos estes ativos)
                private readonly string[] ativosOpostos =
                {
                    "USDJPY",  // par se beneficia de dólar mais forte
                    "USDCHF",
                    "USDCAD"
                };

            
            //  "USDCHF", "USDJPY", "USDCAD"
    
            private readonly string[] ativosGeral;
            private int contadorGlobal;
            
            
    
            private readonly Dictionary<string, double> tpPorAtivo = new Dictionary<string, double>
            {
                {"EURUSD", 0},
                {"GBPUSD", 0},
                {"NZDUSD", 0},
                {"EURGBP", 0},
                {"AUDUSD", 0},
                {"USDCHF", 0},
                {"USDJPY", 0},
                {"USDCAD", 0},
                {"EURJPY", 0},
                {"XAUUSD", 0}
            };
    
            private readonly Dictionary<string, double> slPorAtivo = new Dictionary<string, double>
            {
                {"EURUSD", 0},
                {"GBPUSD", 0},
                {"NZDUSD", 0},
                {"EURGBP", 0},
                {"AUDUSD", 0},
                {"USDCHF", 0},
                {"USDJPY", 0},
                {"USDCAD", 0},
                {"EURJPY", 0},
                {"XAUUSD", 0}
            };
            
            
              private readonly Dictionary<string, double> multiAtivo = new Dictionary<string, double>
            {
                {"EURUSD", 1},
                {"GBPUSD", 1},
                {"NZDUSD", 1},
                {"EURGBP", 1},
                {"AUDUSD", 1},
                {"USDCHF", 1},
                {"USDJPY", 1},
                {"USDCAD", 1},
                {"EURJPY", 1},
                {"XAUUSD", 5}
            };
            
            
            private readonly Dictionary<string, double> difReentradas = new Dictionary<string, double>
            {
                {"EURUSD", -5},
                {"GBPUSD", -5},
                {"NZDUSD", -5},
                {"EURGBP", -5},
                {"AUDUSD", -5},
                {"USDCHF", -5},
                {"USDJPY", -5},
                {"USDCAD", -5},
                {"EURJPY", -5},
                {"XAUUSD", -10}
            };
            
               private readonly Dictionary<string, double> multiTpReentrada = new Dictionary<string, double>
            {
                {"EURUSD", 1.2},
                {"GBPUSD", 1.2},
                {"NZDUSD", 1.2},
                {"EURGBP", 1.2},
                {"AUDUSD", 1.2},
                {"USDCHF", 1.2},
                {"USDJPY", 1.2},
                {"USDCAD", 1.2},
                {"EURJPY", 1.2},
                {"XAUUSD", 1.5}
            };
            
    
            [Parameter("Máx. posições abertas por ativo", DefaultValue = 1)]
            public int MaximoPosicoesPorAtivo { get; set; }
    
            [Parameter("Margin galling", DefaultValue = false)]
            public bool marginGalling { get; set; }
            
            [Parameter("Tipo galling", DefaultValue = 1, MinValue = 1, MaxValue = 4)]
            public int tipoGalling { get; set; }
            
            [Parameter("TP", DefaultValue = 1, MinValue = 1)]
            public int Win { get; set; }
            
            [Parameter("Tp Dinamico ?", DefaultValue = false)]
            public bool tpDinamico { get; set; }
            
            [Parameter("SL", DefaultValue = 1, MinValue = 1)]
            public int Loss { get; set; }
            
            [Parameter("Pode comprar", DefaultValue = true)]
            public bool podeComprar { get; set; }
            
            [Parameter("Pode vender", DefaultValue = true)]
            public bool podeVender { get; set; }
            
             [Parameter("Entrar Ativos Opostos", DefaultValue = true)]
            public bool ativarAtivosOpostos { get; set; }
            
            private const int FavorableCount      = 10;        // número de ativos "a favor"
            private const int OpposingCount       = 10;        // número de ativos "contra"
            
            // Construtor: é aqui que podemos atribuir ao readonly de instância
            public AcceleratorTradingBot()
            {   
                
                ativosGeral = ativos.Concat(ativosOpostos).ToArray();
            }
    
            protected override void OnStart()
            {
                Print("Bot iniciado.");
                ema = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 14);
                ac = Indicators.AcceleratorOscillator();
    
                foreach (var ativo in ativosGeral)
                {
                    contadorLossPorAtivo[ativo] = 0;
                    prejuizoAcumuladoPorAtivo[ativo] = 0.0;
                    ultimaOperacaoIdPorAtivo[ativo] = -1;
                    reentradasPorAtivo[ativo] = 0;
                    entradasPositivas[ativo]  = 0;
                    tpPorAtivo[ativo] = Win;
                    slPorAtivo[ativo] = Loss;
                    contadorGlobal = 0;
                    
                }
    
            
    
                Positions.Closed += OnPositionClosed;
            }
    
            private void OnPositionClosed(PositionClosedEventArgs args)
            {
                var pos = args.Position;
                var ativo = pos.SymbolName;
    
                if (!contadorLossPorAtivo.ContainsKey(ativo))
                    return;
    
                if (pos.Id == ultimaOperacaoIdPorAtivo[ativo])
                    return;
    
                ultimaOperacaoIdPorAtivo[ativo] = pos.Id;
    
            if (pos.GrossProfit < 0)
                {
                    contadorLossPorAtivo[ativo]++;
                    contadorGlobal++;
                    double lossPips = Math.Abs(pos.Pips);               // 500 pips
                    
                
                    foreach (var ativoG in ativosGeral)
                    {
                        double tpPips   = tpPorAtivo[ativoG];            // 10 pips
                        double razao    = lossPips / tpPips ;        // 50/8 ≈ 6.25
                        
                        int entradasNecessarias = Math.Max(1, 
                            (int)Math.Ceiling(razao/ ativosGeral.Length) + 1);               // ceil(6.25)+1 = 7+1 = 8
                
                        reentradasPorAtivo[ativoG] += entradasNecessarias;
                        entradasPositivas[ativoG] = 0;
                    }
                    
                    prejuizoAcumuladoPorAtivo[ativo] += lossPips;        // se quiser acumular em pips
                   }


                else if (pos.GrossProfit > 0)
                {
                   
                    contadorLossPorAtivo[ativo] = 0;
                    prejuizoAcumuladoPorAtivo[ativo] = 0.0;
                    entradasPositivas[ativo]++;
                    
                    
                }
            }
    
            private int ContarPosicoesAtivas(string ativo)
            {
                int count = 0;
                foreach (var pos in Positions)
                {
                    if (pos.SymbolName == ativo)
                        count++;
                }
                return count;
            }
    
        private double ObterProfitPosicaoAbertaEmDolar(string ativo)
            {
                var ultimaAberta = Positions
                    .Where(p => p.SymbolName.Equals(ativo, StringComparison.InvariantCultureIgnoreCase))
                    .OrderByDescending(p => p.EntryTime)   // pega a mais nova
                    .FirstOrDefault();
            
                return ultimaAberta != null
                    ? ultimaAberta.GrossProfit           // negativo se estiver no vermelho
                    : double.NaN;
            }
            private int CalcularForcaDesdeInicioDoDia()
            {
                int contador = 0;
                DateTime hoje = Server.Time.Date;
    
                for (int i = Bars.Count - 1; i >= 0; i--)
                {
                    if (Bars.OpenTimes[i].Date != hoje)
                        break;
    
                    if (Bars.ClosePrices[i] > Bars.OpenPrices[i])
                        contador++;
                    else if (Bars.ClosePrices[i] < Bars.OpenPrices[i])
                        contador--;
                }
                return contador;
            }
    
            private int CalcularForcaV()
            {
                int forca = 0;
    
                if (Bars.ClosePrices.Last(1) < Bars.ClosePrices.Last(2) && Bars.ClosePrices.Last(2) > Bars.ClosePrices.Last(3)){
                    forca = -1;
                 }
                if (Bars.ClosePrices.Last(1) > Bars.ClosePrices.Last(2) && Bars.ClosePrices.Last(2) < Bars.ClosePrices.Last(3)){
                    forca = 1;
                 }
    
                return forca;
            }
    
  
  
  
          
     private void AjustarStopPorPercentual(
    string ativo,
    double limiarPercentual     = 0.50,  // quando ativar (75% do caminho ao TP)
    double slPercentual         = 0.30,  // proteção SL (40% do ganho realizado)
    double tpExtensaoPercentual = 1.01)  // extensão TP (+10% da distância total)
{
    var symbol = Symbols.GetSymbol(ativo);
    if (symbol == null) return;

    var abertas = Positions
        .Where(p => p.SymbolName == ativo && p.GrossProfit > 0);

    foreach (var pos in abertas)
    {
        double entryPrice = pos.EntryPrice;
        TradeType tipo   = pos.TradeType;
        double marketPrice = tipo == TradeType.Buy ? symbol.Ask : symbol.Bid;

        // — TP original (se não existir, pega do tpPorAtivo ou Win)
        double tpPriceAtual = pos.TakeProfit 
            ?? (tipo == TradeType.Buy
                    ? entryPrice + ((tpPorAtivo.ContainsKey(ativo) ? tpPorAtivo[ativo] : Win) * symbol.PipSize)
                    : entryPrice - ((tpPorAtivo.ContainsKey(ativo) ? tpPorAtivo[ativo] : Win) * symbol.PipSize)
               );

        // — distâncias totais e já realizadas
        double distanciaTotal  = Math.Abs(tpPriceAtual - entryPrice);
        double distanciaAtual  = Math.Abs(marketPrice   - entryPrice);
        double difPercentual   = distanciaAtual / distanciaTotal;

        // só entra quando ultrapassar o limiar percentual
        if (difPercentual < limiarPercentual)
            continue;

        // — SL móvel = só 40% do que já andou (nunca ultrapassa o mercado)
        double newSlMovel = tipo == TradeType.Buy
            ? entryPrice + (distanciaAtual * slPercentual)
            : entryPrice - (distanciaAtual * slPercentual);

        // — TP móvel = extensão de 1.10× a distância total
        double newTpMovel = tipo == TradeType.Buy
            ? entryPrice + (distanciaTotal * tpExtensaoPercentual)
            : entryPrice - (distanciaTotal * tpExtensaoPercentual);

       

        // — finalmente aplica
        var resultado = ModifyPosition(pos, newSlMovel, newTpMovel,ProtectionType.Absolute);
        if (resultado.IsSuccessful)
        {
            Print($"[TRAILING] {ativo} {tipo} #{pos.Id} → SL: {newSlMovel:F5}, TP: {newTpMovel:F5}");
        }
        else
        {
            Print($"[ERRO CONFIG] {ativo} #{pos.Id} ({tipo}): {resultado.Error}");
        }
    }
}

    
    
    
    
    
    
    
    
       private double ObterAtrDiario(string ativo, int periodo)
            {
                // Repare: primeiro o TimeFrame, depois o símbolo
                var dailyBars = MarketData.GetBars(TimeFrame.Daily, ativo);
            
                // Use a sobrecarga que recebe Bars diretamente
                var atr = Indicators.AverageTrueRange(
                    dailyBars,                  // Bars diário
                    periodo,                    // período (ex: 14)
                    MovingAverageType.Exponential
                );
            
                // Retorna em unidades de preço (para converter em pips, divida por PipSize)
                return atr.Result.LastValue;
            }

            private bool PermiteOperarAtrDinamico(string ativo, double atrPips, string direction, double objetivoPips)
                {
                    var symbol = Symbols.GetSymbol(ativo);
                    if (symbol == null) return false;
                
                    // Bars diários corretamente
                    var dailyBars = MarketData.GetBars(TimeFrame.Daily, ativo);
                    double openPrice = dailyBars.OpenPrices.LastValue;
                
                    double pipSize  = symbol.PipSize;
                    double bandHigh = openPrice + atrPips * pipSize;
                    double bandLow  = openPrice - atrPips * pipSize;
                
                    double current = direction.Equals("buy", StringComparison.OrdinalIgnoreCase)
                        ? symbol.Bid
                        : symbol.Ask;
                
                    double spacePips = direction.Equals("buy", StringComparison.OrdinalIgnoreCase)
                        ? (bandHigh - current) / pipSize
                        : (current - bandLow)  / pipSize;
                
                    return spacePips >= objetivoPips * 2;
                }

          
            private void DesenharSetaOperacao(string tipo)
            {
                string nome = $"seta_{tipo}_{Server.Time.Ticks}";
                int index = Bars.Count - 1;
    
                double precoY = tipo.ToLower() == "buy"
                    ? Bars.LowPrices[index]
                    : Bars.HighPrices[index];
    
                ChartIconType icone = tipo.ToLower() == "buy"
                    ? ChartIconType.UpArrow
                    : ChartIconType.DownArrow;
    
                Color cor = tipo.ToLower() == "buy" ? Color.Yellow : Color.Aqua;
    
                Chart.DrawIcon(nome, icone, index, precoY, cor);
            }
    
    
       private int IsAccelerating()
        {
            double ac4 = ac.Result.Last(4);
            double ac3 = ac.Result.Last(3);
            double ac2 = ac.Result.Last(2);
            double ac1 = ac.Result.Last(1);
            double ac0 = ac.Result.Last(0);
            
            int retorno = 0;
          
            // Aceleração POSITIVA (tendência de alta se fortalecendo)
            // AC crescente E atual valor positivo = sinal de COMPRA para ativos que seguem tendência
            if(ac1 > ac2 && ac2 > ac3 && ac3 > ac4 && ac0 > 0){
               
                retorno = 1;  // Mudança: agora 1 = BUY
            }
          
            // Aceleração NEGATIVA (tendência de baixa se fortalecendo)  
            // AC decrescente E atual valor negativo = sinal de VENDA para ativos que seguem tendência
            else if(ac1 < ac2 && ac2 < ac3 && ac3 < ac4 && ac0 < 0){  // CORREÇÃO: ac0 < 0
               
                retorno = -1;  // Mudança: agora -1 = SELL
            }
            
            return retorno;
        }

    
            protected override void OnBar()
            {   
            
                int signal = CalcularForcaV();
                if (signal == 0) return;
                
                
                
                
                bool isBuy = signal > 0;         // MUDANÇA: agora 1→compra, -1→venda
                string direction = isBuy ? "buy" : "sell";
                
                 var selectedFavorables = ativos.Take(FavorableCount);
                 var selectedOpposings  = ativarAtivosOpostos 
                              ? ativosOpostos.Take(OpposingCount)
                              : Enumerable.Empty<string>();
                
             
                
             
                 // executa as ordens “a favor” do sinal
                foreach (var symbol in selectedFavorables)
                {   
                
                    ConsultarCompraVenda(symbol, direction, 1);
                    DesenharSetaOperacao(direction);
                }
                
                string oppDirection = isBuy ? "sell" : "buy";
                
                 
                
                foreach (var symbol in selectedOpposings)
                {
                    ConsultarCompraVenda(symbol, oppDirection, 2);
                    DesenharSetaOperacao(oppDirection);
                }
                
                 foreach (var ativo in ativosGeral){
                    AjustarStopPorPercentual(ativo);
                    
                 }
                
            }
    
            private void ConsultarCompraVenda(string ativo, string tipo, double correletion)
            {   
                
                
                int posicoesAbertas = ContarPosicoesAtivas(ativo);
                double profitNegativo = ObterProfitPosicaoAbertaEmDolar(ativo);
                if (posicoesAbertas >= MaximoPosicoesPorAtivo)
                {
                  
                    return;
                }
                
                
               
                if(profitNegativo > difReentradas[ativo] && posicoesAbertas > 0){
                     return;
                }
                    
    
                var symbol = Symbols.GetSymbol(ativo);
                if (symbol == null)
                    return;
                    
            
                
    
                double tpPips = tpPorAtivo.ContainsKey(ativo) ? tpPorAtivo[ativo] : 100;
                double slPips = slPorAtivo.ContainsKey(ativo) ? slPorAtivo[ativo] : 66;
                double volume = symbol.VolumeInUnitsMin;
                double acumulado = prejuizoAcumuladoPorAtivo[ativo];
                double lucroPorVolume = symbol.PipValue * tpPips * volume;
    
                if (lucroPorVolume > 0 && marginGalling)
                {   
                    // Entrada diluida 
                    if(entradasPositivas[ativo] >= 1 && reentradasPorAtivo[ativo] > 0 && tipoGalling == 1){
                        volume *= 2;
                        reentradasPorAtivo[ativo]--;
                        
                        if(reentradasPorAtivo[ativo]  <= 0 ){
                            reentradasPorAtivo[ativo] = 0;
                        }
                    }
                    
                    if( contadorLossPorAtivo[ativo] > 0 && tipoGalling == 2){
            
                        int multiplicador = (int)((acumulado / lucroPorVolume) ) + 1;
                        volume *= multiplicador;
                        
                    }
                    
                    
                    if(ativo == "USDJPY" && entradasPositivas[ativo] >= 1 && contadorGlobal > 0 && tipoGalling == 3){
                        volume *= 30;
                        
                        contadorGlobal = 0;
                        
                    }
                    
                    
                    if(contadorLossPorAtivo[ativo] > 0 && tipoGalling == 4){
                        
                         volume *= 3;
                        reentradasPorAtivo[ativo]--;
                        
                        if(reentradasPorAtivo[ativo]  <= 0 ){
                            reentradasPorAtivo[ativo] = 0;
                        }
                        
                    }
                }
                
                
                
                // se tpDinamico faça:
                
                if(tpDinamico){        
                double atrPrice = ObterAtrDiario(ativo, 14);
                double atrPips  = atrPrice / symbol.PipSize;
                double tpPipsDinamicoAtr = atrPips * 0.25;
                
                bool podeOperar =  PermiteOperarAtrDinamico(ativo, atrPips, tipo, tpPipsDinamicoAtr);
               
                    if(!podeOperar){
                        //Print("Sem espaço para Operar:", ativo );
                        return;    
                    }else{
                        tpPips = tpPipsDinamicoAtr;    
                    }
                
                }
                
                tpPips *= multiAtivo[ativo];
                
                if(posicoesAbertas > 0){
                    tpPips *= (posicoesAbertas * multiTpReentrada[ativo]);
                }
                if (tipo == "buy" && podeComprar)
                 {
                        ExecuteMarketOrder(TradeType.Buy, ativo, volume, $"{posicoesAbertas}-BUY- {correletion}", slPips, tpPips);
                   
                }
                if(tipo == "sell" && podeVender && ativo != "XAUUSD"){
                     
                        ExecuteMarketOrder(TradeType.Sell, ativo, volume, $"{posicoesAbertas}SELL- {correletion}", slPips, tpPips);
                } 
            }
        }
    }
