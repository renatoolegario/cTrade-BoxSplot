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

        
        [Parameter("Dif Rentradas", DefaultValue = -1.0, MinValue = -50, MaxValue = 0)]
        public double DifRentradas { get; set; }        
        
        [Parameter("TP Minimo", DefaultValue = 5 , MinValue = 0, MaxValue = 1000)]
        public int difMinima { get; set; }
        
        [Parameter("Volume MAX", DefaultValue = 5 , MinValue = 0, MaxValue = 100)]
        public int volumeMax { get; set; }
        
        private double ultimaLinhaDesenhada = double.NaN;
      
      
        protected override void OnStart()
        {
            Print("Bot iniciado.");
            ac = Indicators.AcceleratorOscillator();
            
        }

        private double GetAccountBalance()
        {
             double balance = Account.Equity;
             return balance;
          
            
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



   


        private bool IsAccelerating(bool tipo)
        {
        
            double ac2 = ac.Result.Last(2);
            double ac1 = ac.Result.Last(1);
            double ac0 = ac.Result.Last(0);
            
            if(tipo){
                return !double.IsNaN(ac2) && !double.IsNaN(ac1) && !double.IsNaN(ac0) &&
                   ac2 < ac1 && ac1 < ac0 && ac0 < 0;
            }else{
                return !double.IsNaN(ac2) && !double.IsNaN(ac1) && !double.IsNaN(ac0) &&
                   ac2 > ac1 && ac1 > ac0 && ac0 > 0;
            }
        }

        protected override void OnBar()
        {
            if (IsAccelerating(true))
            {
                
                DrawOrUpdateHorizontalLine("AC BUY", Symbol.Bid, Color.Yellow);
                ConsultarCompra();
            }
            
            
            
            

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


           
        // COMPRAS 
        private void ConsultarCompra( )
        {
            double price = Symbol.Bid;
            int ordensCompra = ContarOrdensAtivoAtual();            
            double profit = GetOldestOpenProfit();           
            bool aprova = true;
            
            double volume = Symbol.VolumeInUnitsMin;
            double tpPips = difMinima;
            
            if(ordensCompra > 0){
                aprova = profit < DifRentradas * ordensCompra;
                
                tpPips = tpPips * ordensCompra;
                
                if(ordensCompra + 1 > volumeMax){
                    volume = volume * volumeMax;
                }else{
                    volume = volume * (1 + ordensCompra);
                }
                
            }
               
            double conta = GetAccountBalance();
            int ordMax = (int)Math.Floor(conta / ValorPorOperacao);
            
            
          
            
             if(ordensCompra < ordMax && aprova ){
                // COMPRAS NO GAP CONTRA TENDENCIA
            
                 
                  
                    var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume , "BUY " + ordensCompra, null, tpPips);
                    if (result.IsSuccessful)
                        Print($"✅ COMPRA no toque do TP ");
                    else
                        Print($"❌ Erro na compra no MIN: {result.Error}");
              
          
            } 
            
            
            
            
            
            
            
            
        }

      


    }
}