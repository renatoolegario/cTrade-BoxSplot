    using cAlgo.API;
    using cAlgo.API.Indicators;
    using System;
    using System.Collections.Generic;
    using System.Linq; 
    
    namespace cAlgo.Robots
    {
        [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
        public class AcceleratorTradingBot : Robot
        {

        #region Campos e Constantes

        private ExponentialMovingAverage ema;
            private AcceleratorOscillator ac;
            private Dictionary<string, int> contadorLossPorAtivo = new Dictionary<string, int>();
            private Dictionary<string, double> prejuizoAcumuladoPorAtivo = new Dictionary<string, double>();
            private Dictionary<string, long> ultimaOperacaoIdPorAtivo = new Dictionary<string, long>();
            private Dictionary<string, int> reentradasPorAtivo = new Dictionary<string, int>();
            private Dictionary<string, int> entradasPositivas = new Dictionary<string, int>();
            private readonly Queue<int> forcaVHistory = new Queue<int>(capacity: 5);
            
            private int consecutiveUpCount = 0;
            private int consecutiveDownCount = 0;
            private double currentMaxHigh = double.MinValue;
            private double currentMinLow = double.MaxValue;
            private const string TopLineName = "TopLine";
            private const string BottomLineName = "BottomLine";

            
            // Ativos para operar “contra” a USDX (quando o USDX cai, compramos estes ativos)
                private readonly string[] ativos =
                {
                    "EURUSD", 
                    "GBPUSD",
                    "AUDUSD",
                    "NZDUSD",                   
                    "EURJPY",
                    "XAUUSD",  
                    "XAGUSD",
                   
                    
                };
                
                // Ativos para operar “a favor” da USDX (quando o USDX sobe, compramos estes ativos)
                private readonly string[] ativosOpostos =
                {
                
                    "USDJPY",
                };

            
         
            private readonly string[] ativosGeral;
            private int contadorGlobal;
            
            
             [Parameter("Estratégia", DefaultValue = TipoCalculo.IsAccelerating)]
             public TipoCalculo estrategia { get; set; }
            
            
            // --- Parâmetros para habilitar/desabilitar entrada em cada ativo ---
            [Parameter("Entrar EURUSD", DefaultValue = true)]
            public bool PodeEntrarEURUSD { get; set; }
            
            [Parameter("Entrar GBPUSD", DefaultValue = true)]
            public bool PodeEntrarGBPUSD { get; set; }
            
            [Parameter("Entrar NZDUSD", DefaultValue = true)]
            public bool PodeEntrarNZDUSD { get; set; }
            
            [Parameter("Entrar AUDUSD", DefaultValue = true)]
            public bool PodeEntrarAUDUSD { get; set; }
            
            [Parameter("Entrar EURJPY", DefaultValue = true)]
            public bool PodeEntrarEURJPY { get; set; }
            
            [Parameter("Entrar XAUUSD", DefaultValue = true)]
            public bool PodeEntrarXAUUSD { get; set; }
            
            [Parameter("Entrar XAGUSD", DefaultValue = true)]
            public bool PodeEntrarXAGUSD { get; set; }
            
            [Parameter("Entrar USDJPY", DefaultValue = true)]
            public bool PodeEntrarUSDJPY { get; set; }
            
            // ----------------------------------------------------------------------

                       
            private const bool tpDinamico = true;
            
            private const int Win = 10;
            private const int Loss = 10000;
            
            private const int FavorableCount      = 10;        // número de ativos "a favor"
            private const int OpposingCount       = 10;        // número de ativos "contra"
            
            
            private double ultimaLinhaDesenhada = double.NaN;
            
            public enum TipoCalculo
            {
                IsAccelerating,
                CalcularForcaDesdeInicioDoDia,
                CalcularForcaV,
                CalcularForcaVAcumulada,
                RompimentoTopoFundo,
                Nenhuma
            }
            
    
            public AcceleratorTradingBot(){  ativosGeral = ativos.Concat(ativosOpostos).ToArray(); }


        #endregion

        #region Configurações por Ativo
        
        //vamos criar um dictionary para saber quais pode comprar e quais pode vender ativo por ativo.
             
             private readonly Dictionary<string, bool> ativoPodeComprar = new Dictionary<string, bool>
            {
                {"EURUSD", true},
                {"GBPUSD", true},                
                {"AUDUSD", true},                
                {"XAUUSD", true},   
                {"NZDUSD", true},
                {"EURJPY", true},                 
                {"XAGUSD", true},                               
                {"USDJPY", false},
            };  
            
            private readonly Dictionary<string, bool> ativoPodeVender = new Dictionary<string, bool>
            {
                {"EURUSD", false},
                {"GBPUSD", false},                
                {"AUDUSD", false},                
                {"XAUUSD", false},
                {"NZDUSD", false},
                {"EURJPY", true}, 
                {"XAGUSD", true},
                {"USDJPY", true},
                
            };
                
                
              private readonly Dictionary<string, double> entradasPermitidas = new Dictionary<string, double>
            {
                {"EURUSD", 2},
                {"GBPUSD", 2},
                {"NZDUSD", 2},
                {"AUDUSD", 2},
                {"EURJPY", 2},
                {"XAUUSD", 1},                
                {"XAGUSD", 1},
                {"USDJPY", 2},
                
            };
            
            private readonly Dictionary<string, double> tpPorAtivo = new Dictionary<string, double>
            {
                {"EURUSD", 0},
                {"GBPUSD", 0},
                {"NZDUSD", 0},
                {"AUDUSD", 0},
                {"EURJPY", 0},
                {"XAUUSD", 0},                
                {"XAGUSD", 0},                               
                {"USDJPY", 0},
            };
    
            private readonly Dictionary<string, double> slPorAtivo = new Dictionary<string, double>
            {
                {"EURUSD", 0},
                {"GBPUSD", 0},
                {"NZDUSD", 0},
                {"AUDUSD", 0},
                {"EURJPY", 0},
                {"XAUUSD", 0},                               
                {"XAGUSD", 0},                
                {"USDJPY", 0},
            };
            
            
              private readonly Dictionary<string, double> multiAtivo = new Dictionary<string, double>
            {
                {"EURUSD", 3},
                {"GBPUSD", 3},
                {"NZDUSD", 3},
                {"AUDUSD", 3},
                {"EURJPY", 3},
                {"XAUUSD", 10},                
                {"XAGUSD", 10},                
                {"USDJPY", 3},
            };
            
            
            private readonly Dictionary<string, double> difReentradas = new Dictionary<string, double>
            {
                {"EURUSD", -5},
                {"GBPUSD", -5},
                {"NZDUSD", -1},
                {"AUDUSD", -1},
                {"EURJPY", -1},
                {"XAUUSD", -50},                
                {"XAGUSD", -50},                
                {"USDJPY", -3},
            };
            
               private readonly Dictionary<string, double> multiTpReentrada = new Dictionary<string, double>
            {
                {"EURUSD", 1.2},
                {"GBPUSD", 1.2},
                {"NZDUSD", 1.2},
                {"AUDUSD", 1.2},
                {"EURJPY", 1.2},
                {"XAUUSD", 1.5},                
                {"XAGUSD", 1.5},  
                {"USDJPY", 1.2},
            };

        
        #endregion


        #region Inicializador do código
        
        
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
                 Timer.Start(5000);
            }


        protected override void OnTimer()
        {
        
             foreach (var ativo in ativosGeral){
                    AjustarStopPorPercentual(ativo);
                    
                 }
                
            base.OnTimer();
        }

        protected override void OnBar()
            {   
                
                AtualizarToposBottom();
                int signal = GetSignal(estrategia);
                
                
                if (signal == 0) return;
                
                
                
                bool isBuy = signal > 0;         // MUDANÇA: agora 1→compra, -1→venda
                string direction = isBuy ? "buy" : "sell";
                
                 var selectedFavorables = ativos.Take(FavorableCount);
                 var selectedOpposings  = ativosOpostos.Take(OpposingCount) ;
                           
                
             
                
             
                 // executa as ordens “a favor” do sinal
                foreach (var symbol in selectedFavorables)
                {   
                    
                    if(VerificarPodeEntrar(symbol)){
                        ConsultarCompraVenda(symbol, direction, 1);
                    }
                }
                
                DesenharSetaOperacao(direction);
                string oppDirection = isBuy ? "sell" : "buy";
                
                 
                
                foreach (var symbol in selectedOpposings)
                {   
                    if(VerificarPodeEntrar(symbol)){
                        ConsultarCompraVenda(symbol, oppDirection, 2);
                    }
                }
                
                foreach (var ativo in ativosGeral){
                    AjustarStopPorPercentual(ativo);
                    
                 }
                
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
            
            
            
                   
    
    public int GetSignal(TipoCalculo estrategia)
    {
        switch (estrategia)
        {
            case TipoCalculo.CalcularForcaV:
                return CalcularForcaV();
            case TipoCalculo.CalcularForcaVAcumulada:
                return CalcularForcaVAcumulada();
            case TipoCalculo.CalcularForcaDesdeInicioDoDia:
                return CalcularForcaDesdeInicioDoDia();
            case TipoCalculo.IsAccelerating:
                return IsAccelerating();
            case TipoCalculo.RompimentoTopoFundo:
                return RompimentoTopoFundo();
        
            default:
                return 0;
        }
    }
    
  private void DrawOrUpdateHorizontalLine(string name, double price, Color cor)
        {
            // Se o preço não mudou, não redesenha (evita flickering)
            if (Math.Abs(ultimaLinhaDesenhada - price) < Symbol.PipSize)
                return;

            // Remove a linha anterior (se existir)
            Chart.RemoveObject(name);

            // Desenha nova linha
            Chart.DrawHorizontalLine(name, price, cor, 2, LineStyle.Solid);

            // Armazena valor desenhado
            ultimaLinhaDesenhada = price;

        }

        #endregion


        #region Consultas de Posições e Conta
        
     
        
        
          private void AtualizarToposBottom()
        {
           int consultaAcumulado = CalcularForcaVAcumulada();
                
                if(consultaAcumulado == -1 ){
               
                    consecutiveUpCount++;
                    if(consecutiveUpCount >= 3){
                     
                         consecutiveDownCount = 0;
                        //gaurdo o o topo e atualizo a linha
                          currentMaxHigh = Symbol.Ask;
                          DrawOrUpdateHorizontalLine(TopLineName, Symbol.Ask, Color.Yellow);
                          
                    }
                }
                if(consultaAcumulado == 1 ){
                    consecutiveDownCount++;
                        if(consecutiveDownCount >= 3){
                      
                         consecutiveUpCount = 0;
                        //gaurdo o o topo e atualizo a linha
                          currentMinLow = Symbol.Bid;
                          DrawOrUpdateHorizontalLine(BottomLineName, Symbol.Bid, Color.Yellow);
                         
                    }
                }
                
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
                    double lossPips = Math.Abs(pos.Pips);              
                    
                
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
            
            
          
        #endregion


        #region Consulta e Permisões de Ativos

     
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


  public bool VerificarPodeEntrar(string simbolo)
    {
        // Converte para maiúsculas para garantir que o usuário possa enviar "eurusd", "EURUSD" ou "Eurusd"
        switch (simbolo.Trim().ToUpperInvariant())
        {
            case "EURUSD":
                return PodeEntrarEURUSD;
            case "GBPUSD":
                return PodeEntrarGBPUSD;
            case "NZDUSD":
                return PodeEntrarNZDUSD;
            case "AUDUSD":
                return PodeEntrarAUDUSD;
            case "EURJPY":
                return PodeEntrarEURJPY;
            case "XAUUSD":
                return PodeEntrarXAUUSD;
            case "XAGUSD":
                return PodeEntrarXAGUSD;
            case "USDJPY":
                return PodeEntrarUSDJPY;
            default:
                // Se o símbolo não estiver na lista, optamos por retornar false.
                // Você pode também lançar uma exceção ou logar uma mensagem de erro, conforme a sua lógica.
                return false;
        }
    }
        #endregion



        #region Estratégias

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
            

            private int CalcularForcaVAcumulada()
                {
                    // 1) Obter o sinal “puro” e enfileirar
                    int sinalAtual = CalcularForcaV();
                    forcaVHistory.Enqueue(sinalAtual);
                    if (forcaVHistory.Count > 5)
                        forcaVHistory.Dequeue();
                    
                    // 2) Se ainda não há 5 valores, retorna 0
                    if (forcaVHistory.Count < 5)
                        return 0;
                
                    // 3) Transforma a fila em array; o índice 0 é o elemento MAIS ANTIGO
                    //    e o índice 4 é o elemento MAIS RECENTE.
                    var arraySinais = forcaVHistory.ToArray();
                    double somaPonderada = 0;
                
                    // 4) Atribuímos pesos crescentes de 1 até 5.
                    //    Peso 1 → arraySinais[0] (mais antigo)
                    //    Peso 5 → arraySinais[4] (mais recente)
                    for (int i = 0; i < 5; i++)
                    {
                        int peso = i + 1;           // i=0 → peso=1, i=4 → peso=5 (mais forte)
                        somaPonderada += arraySinais[i] * peso;
                    }
                    
                    // 5) Retorna o sinal final
                    if (somaPonderada > 0)
                        return 1;
                    else if (somaPonderada < 0)
                        return -1;
                    else
                        return 0;
                }

          private int RompimentoTopoFundo()
            {
                int value = 0;
            
                // Fecha da vela anterior:
                double closeAnterior = Bars.ClosePrices.Last(1);
               
               int statusForca = CalcularForcaV();
               
                
               if(statusForca == 0){
                return 0;
               }
                
                if (closeAnterior > currentMaxHigh && statusForca == -1 && currentMaxHigh > double.MinValue)
                {
                    value = -1;  // Rompimento de topo → sinal de alta
                }
                
                else if (closeAnterior < currentMinLow && statusForca == 1 && currentMinLow < double.MaxValue) 
                {
                    value = 1;  // Rompimento de fundo → sinal de baixa
                }
            
                return value;
            }
            
            
            
        


        #endregion




        #region Analise de Compras e vendas e Stop Movel

private void AjustarStopPorPercentual(string ativo)
{
    var symbol = Symbols.GetSymbol(ativo);
    if (symbol == null) return;
    
    double distanciaMinima = 1;
    double profity = 2;
   if(ativo == "XAUUSD" || ativo == "USDX" || ativo == "USDCAD"){
    distanciaMinima = 3;
    profity = 5;
   }
   
  

    // Seleciona posições abertas para este ativo com lucro > 2 (USD ou equivalente)
    var abertas = Positions
        .Where(p => p.SymbolName == ativo && p.GrossProfit > profity);

    double pipSize = symbol.PipSize;

    foreach (var pos in abertas)
    {
        double entryPrice = pos.EntryPrice;
        TradeType tipo    = pos.TradeType;
        double marketPrice = tipo == TradeType.Buy ? symbol.Bid : symbol.Ask; 
        double newSlMovel  = 0;
        double slAtual     = pos.StopLoss ?? entryPrice;  // se não houver SL, assume entry
      

        // Para BUY: SL deve ficar marketPrice - (distanciaMinima * pipSize)
        // Para SELL: SL deve ficar marketPrice + (distanciaMinima * pipSize)
        if (tipo == TradeType.Buy)
            newSlMovel = marketPrice - (distanciaMinima * pipSize);
        else
            newSlMovel = marketPrice + (distanciaMinima * pipSize);

        // Garante que, para BUY, new SL nunca fique abaixo de entryPrice + (distanciaMinima * pipSize)
        // e para SELL, new SL nunca fique acima de entryPrice - (distanciaMinima * pipSize)
        if (tipo == TradeType.Buy)
        {
            double minimoPermitido = entryPrice + (distanciaMinima * pipSize);
            if (newSlMovel <= minimoPermitido)
                newSlMovel = minimoPermitido;
        }
        else // SELL
        {
            double maximoPermitido = entryPrice - (distanciaMinima * pipSize);
            if (newSlMovel >= maximoPermitido)
                newSlMovel = maximoPermitido;
        }

        // Só modifica se o novo SL for "mais favorável" (ou seja, garante lucro mínimo maior)
        bool deveAjustar = tipo == TradeType.Buy
            ? newSlMovel > slAtual
            : newSlMovel < slAtual;

        if (!deveAjustar)
            continue;

        // Mantém o TP atual (não altera)
        double tpAtual = pos.TakeProfit ?? 0;

        var resultado = ModifyPosition(pos, newSlMovel, tpAtual, ProtectionType.Absolute);
        if (resultado.IsSuccessful)
            Print($"[TRAILING] {ativo} {tipo} #{pos.Id} → SL atualizado para {newSlMovel:F5}");
        else
            Print($"[ERRO CONFIG] {ativo} #{pos.Id} ({tipo}): {resultado.Error}");
    }
}


    private void ConsultarCompraVenda(string ativo, string tipo, double correletion)
            {   
                
                
                int posicoesAbertas = ContarPosicoesAtivas(ativo);
                double profitNegativo = ObterProfitPosicaoAbertaEmDolar(ativo);
                if (posicoesAbertas >= entradasPermitidas[ativo])
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
              
                
                // se tpDinamico faça:
                
                if(tpDinamico && ativo != "XAUUSD" && ativo != "USDX"){        
                double atrPrice = ObterAtrDiario(ativo, 14);
                double atrPips  = atrPrice / symbol.PipSize;
                double tpPipsDinamicoAtr = atrPips * 0.25;
                
                bool podeOperar =  PermiteOperarAtrDinamico(ativo, atrPips, tipo, tpPipsDinamicoAtr);
               
                    if(!podeOperar){
                        return;    
                    }else{
                        tpPips = tpPipsDinamicoAtr;    
                    }
                
                }
                
                tpPips *= multiAtivo[ativo];
                
                if(posicoesAbertas > 0){
                    tpPips *= (posicoesAbertas * multiTpReentrada[ativo]);
                }
                if (tipo == "buy" &&  ativoPodeComprar[ativo] == true)
                 {
                        ExecuteMarketOrder(TradeType.Buy, ativo, volume, $"{posicoesAbertas}", null, tpPips);
                   
                }
                if(tipo == "sell" &&  ativoPodeVender[ativo] == true){
                     
                        ExecuteMarketOrder(TradeType.Sell, ativo, volume, $"{posicoesAbertas}", null, tpPips);
                } 
            }
        }
 
    
    
    

        #endregion




      }
         
