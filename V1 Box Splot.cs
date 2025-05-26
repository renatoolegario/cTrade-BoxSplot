using cAlgo.API;
using cAlgo.API.Indicators;
using System;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class AcceleratorTradingBot : Robot
    {
        private AcceleratorOscillator ac;

        [Parameter("Valor por Operação (USD)", DefaultValue = 50.0, MinValue = 0.1)]
        public double ValorPorOperacao { get; set; }


        [Parameter("Limite Capital (USD)", DefaultValue = 250.0, MinValue = 0.0)]
        public double LimiteCapital { get; set; }
        
        [Parameter("Quantidade Barras", DefaultValue = 100, MinValue = 10, MaxValue = 100)]
        public int qtdBarras { get; set; }
        
        [Parameter("Pode Comprar?", DefaultValue = true)]
        public bool podeComprar { get; set; }        
        
        
        [Parameter("Comprar na Tendencia ?", DefaultValue = false)]
        public bool comprarTendencia { get; set; }
        
        
        
        [Parameter("Comprar GAP?", DefaultValue = false)]
        public bool comprarGap { get; set; }
       
        
        
        [Parameter("Pode vender?", DefaultValue = true)]
        public bool podeVender { get; set; }
        
        [Parameter("Vender na Tendencia?", DefaultValue = false)]
        public bool venderTendencia { get; set; }
        
        
        
        [Parameter("Vender GAP?", DefaultValue = false)]
        public bool venderGap { get; set; }
        
        [Parameter("Dif Rentradas", DefaultValue = -1.0, MinValue = -10, MaxValue = 0)]
        public double DifRentradas { get; set; }

        [Parameter("Escalar Rentradas", DefaultValue = false)]
        public bool escalarRentrada { get; set; }
        
        [Parameter("Multi Rentradas", DefaultValue = 1, MinValue = 1, MaxValue = 10)]
        public double multiRentradas { get; set; }
        
        [Parameter("Dif Minima Entradas", DefaultValue = 5 , MinValue = 5, MaxValue = 1000)]
        public int difMinima { get; set; }
        
        
        [Parameter("Fixar Dif Minima", DefaultValue = false)]
        public bool fixarDifMinima { get; set; }
        
        
        private double ultimaLinhaDesenhada = double.NaN;
        
 

    
        
        protected override void OnStart()
        {
            Print("Bot iniciado.");
            ac = Indicators.AcceleratorOscillator();
            
        }

        private double GetAccountBalance()
        {
            double balance = Math.Min(Account.Equity, LimiteCapital);
            
            if(balance > LimiteCapital){
                return LimiteCapital;
            }else{
                return balance;
            }
            
            
        }


       

       private double GetOldestOpenProfit()
            {   
                double profit = 0;
                foreach (var position in Positions)
                {
                    if (position.SymbolName == SymbolName)
                        profit = position.NetProfit;
                }
            
                return profit;
            }

       private void DrawOrUpdateHorizontalLine(string name, double price, Color cor)
            {
                // Se o preço não mudou, não redesenha (evita flickering)
                if (Math.Abs(ultimaLinhaDesenhada - price) < Symbol.PipSize)
                    return;
            
                // Remove a linha anterior (se existir)
                Chart.RemoveObject(name);
            
                // Desenha nova linha
                Chart.DrawHorizontalLine(name, price,cor, 2, LineStyle.Solid);
            
                // Armazena valor desenhado
                ultimaLinhaDesenhada = price;
            
            }



     
       private (double Q1, double Q3, double LMMaximo, double LMMinimo) BoxSpot(int quantidade)
            {   
             
                //if (Bars.Count < quantidade) { return (0, 0, 0, 0);  } 
                    
            
                var precos = new double[quantidade];
                for (int i = 0; i < quantidade; i++)
                    precos[i] = Bars.ClosePrices[Bars.Count - 1 - i];
            
                Array.Sort(precos);
            
                double Q1 = Percentil(precos, 25);
                double Q3 = Percentil(precos, 75);
                double IQR = Q3 - Q1;
            
                double LMMaximo = Q3 + 1.5 * IQR;
                double LMMinimo = Q1 - 1.5 * IQR;
            
                return (Q1, Q3, LMMaximo, LMMinimo);
            }

            
         private double Percentil(double[] sortedArray, double percent)
            {
                // Ex: percent = 25 para Q1
                int N = sortedArray.Length;
                if (N == 0) return 0;
            
                double n = (N - 1) * percent / 100.0;
                int k = (int)n;
                double d = n - k;
            
                if (k + 1 < N)
                    return sortedArray[k] + d * (sortedArray[k + 1] - sortedArray[k]);
                else
                    return sortedArray[k];
            }



        protected override void OnBar()
        {   
         
            
            var (q1, q3, max, min) = BoxSpot(qtdBarras);
           
            double price = Symbol.Bid;
         

            
             DrawOrUpdateHorizontalLine("Max", max, Color.Red);
             
            DrawOrUpdateHorizontalLine("Q1", q1, Color.Yellow); 
            DrawOrUpdateHorizontalLine("Q3", q3, Color.Yellow); 

             
             DrawOrUpdateHorizontalLine("Min", min,Color.Red);
             
        
            ConsultarCompraVenda(price, min, max, q1, q3);
            
            
        
        }



    private int ContarOrdensAtivoAtual()
        {
            int count = 0;
            foreach (var pos in Positions)
            {
                if (pos.SymbolName == SymbolName)
                    count++;
            }
            return count;
        }


           

        private void ConsultarCompraVenda( double price, double min, double max, double q1, double q3)
        {
            double volume = Symbol.VolumeInUnitsMin;
            int ordensCompra = ContarOrdensAtivoAtual();
            
            double profit = GetOldestOpenProfit();
            
           
            bool aprova = true;
            bool aprovaDifMinima = true;
            
            if(ordensCompra > 0){
                aprova = profit < DifRentradas * ordensCompra ;
            }
               
            double conta = GetAccountBalance();
            int ordMax = (int)Math.Floor(conta / ValorPorOperacao);
            
            double tpPips = 0;
            if(escalarRentrada && ordensCompra > 0){
              tpPips = Math.Abs((q1 - price) * multiRentradas) / Symbol.PipSize;                      
            }else{
              tpPips = Math.Abs(q1 - price) / Symbol.PipSize;
            }
            
            if(tpPips < difMinima){
                if(fixarDifMinima){
                    tpPips = difMinima;
                }else{
                    aprovaDifMinima =false;
                }
            }
            
             if(ordensCompra < ordMax && aprova && aprovaDifMinima){
                // COMPRAS NO GAP CONTRA TENDENCIA
                
                // ⚡ Parametros Normais de Compra
                
                if (Math.Abs(price - min) <= Symbol.PipSize * 2 && podeComprar && comprarGap)
                {
                 
                  
                    var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, "BUY " + ordensCompra, null, tpPips);
                    if (result.IsSuccessful)
                        Print($"✅ COMPRA no toque do MIN ({min:F5}) | TP em Q1: {q1:F5}");
                    else
                        Print($"❌ Erro na compra no MIN: {result.Error}");
                }
            
                // ⚡ Parametros Normais de Venda
                if (Math.Abs(price - max) <= Symbol.PipSize * 2 && podeVender && venderGap)
                {
                   
                    var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, volume, "SELL "+ ordensCompra, null, tpPips);
                    if (result.IsSuccessful)
                        Print($"✅ VENDA no toque do MAX ({max:F5}) | TP em Q3: {q3:F5}");
                    else
                        Print($"❌ Erro na venda no MAX: {result.Error}");
                }
                
              
            
          
            }
            
            
            
            
             if(ordensCompra < ordMax && aprova){
                // COMPRAS A FAVOR DA  TENDENCIA
                
               
                
                
                // Comprar na Tendencia
                
                if (Math.Abs(price - max) <= Symbol.PipSize * 2 && comprarTendencia && podeComprar)
                {
                   
                    var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, "BUY T" + ordensCompra, null, tpPips);
                    if (result.IsSuccessful)
                        Print($"✅ COMPRA no toque do MAX ({max:F5}) | TP em Q1: {q1:F5}");
                    else
                        Print($"❌ Erro na compra no MAX: {result.Error}");
                }
                
                
                
                // Vender na Tendencia
                
                 if (Math.Abs(price - min) <= Symbol.PipSize * 2 && venderTendencia && podeVender)
                {
                    
                    var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, volume, "SELL T"+ ordensCompra, null, tpPips);
                    if (result.IsSuccessful)
                        Print($"✅ VENDA no toque do MIN ({min:F5}) | TP em Q3: {q3:F5}");
                    else
                        Print($"❌ Erro na venda no MIN: {result.Error}");
                }
            
          
            }
            
            
            
            
            
            
            
            
        }
    }
}