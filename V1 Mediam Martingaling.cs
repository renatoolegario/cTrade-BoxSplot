using cAlgo.API;
using cAlgo.API.Indicators;
using System;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class AcceleratorTradingBot : Robot
    {
        private ExponentialMovingAverage ema;

        [Parameter("Valor por Operação (USD)", DefaultValue = 50.0, MinValue = 0.1)]
        public double ValorPorOperacao { get; set; }
        
        [Parameter("Volume Inicial", DefaultValue = 1, MinValue = 1)]
        public int volumeInicial { get; set; }

        [Parameter("TP", DefaultValue = 3, MinValue = 1)]
        public int tpPoints { get; set; }

        [Parameter("SL", DefaultValue = 1, MinValue = 0)]
        public int slPoints { get; set; }

        [Parameter("Periodo EMA", DefaultValue = 250, MinValue = 1)]
        public int periodoEma { get; set; }

        private bool valorAcimaMedia = false;
        private double BoxSize = 0.00;
        private double ultimaLinhaDesenhada = double.NaN;

        private int contadorLoss = 0;
        private double prejuizoAcumulado = 0.0;

        private ChartText contadorLossText;
        private bool aguardandoFechamento = false;
        private int ultimaOperacaoId = -1;

        protected override void OnStart()
        {
            Print("Bot iniciado.");
            ema = Indicators.ExponentialMovingAverage(Bars.ClosePrices, periodoEma);
            Positions.Closed += OnPositionClosed;
            AtualizarContadorLossNoGrafico();
            Print($"Contador de loss inicializado: {contadorLoss}");
        }

        private double GetAccountBalance() => Account.Equity;

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

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos.SymbolName != SymbolName || pos.Id == ultimaOperacaoId)
                return;

            ultimaOperacaoId = pos.Id;

            Print($"Posição fechada - ID: {pos.Id}, Símbolo: {pos.SymbolName}, Tipo: {pos.TradeType}, " +
                  $"Lucro Bruto: {pos.GrossProfit:F2}, Lucro Líquido: {pos.NetProfit:F2}");

            if (pos.GrossProfit < 0)
            {
                contadorLoss++;
                prejuizoAcumulado += Math.Abs(pos.GrossProfit);
                Print($"LOSS registrado! Total de perdas consecutivas: {contadorLoss}, Prejuízo acumulado: {prejuizoAcumulado:F2}");
            }
            else if (pos.GrossProfit > 0)
            {
                if (contadorLoss > 0 || prejuizoAcumulado > 0)
                    Print($"WIN registrado! Resetando contador de {contadorLoss} e prejuízo de {prejuizoAcumulado:F2} para 0");

                contadorLoss = 0;
                prejuizoAcumulado = 0.0;
            }
            else
            {
                Print("Operação em break-even - mantendo contador e prejuízo");
            }

            AtualizarContadorLossNoGrafico();
            aguardandoFechamento = false;
        }

        private void AtualizarContadorLossNoGrafico()
        {
            string texto = $"Perdas Consecutivas: {contadorLoss}";

            if (contadorLossText == null)
            {
                contadorLossText = Chart.DrawText("contadorLossLabel", texto,
                    Bars.OpenTimes.Last(3), Bars.HighPrices.Last(3), Color.Red);
            }
            else
            {
                contadorLossText.Text = texto;
                contadorLossText.Time = Bars.OpenTimes.Last(0);
                contadorLossText.Y = Bars.HighPrices.Last(0);
            }
        }

        private void DrawOrUpdateHorizontalLine(string name, double price, Color cor)
        {
            if (Math.Abs(ultimaLinhaDesenhada - price) < Symbol.PipSize)
                return;

            Chart.RemoveObject(name);
            Chart.DrawHorizontalLine(name, price, cor, 2, LineStyle.Solid);
            ultimaLinhaDesenhada = price;
        }

        protected override void OnBar()
        {
            AtualizarContadorLossNoGrafico();

            BoxSize = Math.Abs(Bars.ClosePrices.Last(1) - Bars.OpenPrices.Last(1));
            double barraMenos1 = Bars.ClosePrices.Last(1);
            double emaAnterior = ema.Result.Last(1);
            double barraMenos2 = Bars.ClosePrices.Last(2);
            double barraMenos3 = Bars.ClosePrices.Last(3);

            valorAcimaMedia = barraMenos1 > emaAnterior;
            int ordensAbertas = ContarOrdensAtivoAtual();
            int ordensCompra = ordensAbertas;
            double volume = Symbol.VolumeInUnitsMin;
            double conta = GetAccountBalance();
            int ordMax = (int)Math.Floor(conta / ValorPorOperacao);

            double tpPips = tpPoints * BoxSize / Symbol.PipSize;
            double slPips = slPoints * BoxSize / Symbol.PipSize;
            
            if (contadorLoss > 0 && slPips > 0)
            {
                double lucroPorVolume = Symbol.PipValue * slPips * volume;
            
                if (lucroPorVolume > 0)
                {
                    int multiplicador = (int)((prejuizoAcumulado / lucroPorVolume) ) + 1;
                    volume *= multiplicador;
            
                }
            }else{
            
                volume *= volumeInicial;
            
            }


            if (!aguardandoFechamento)
            {
                if (valorAcimaMedia && barraMenos1 > barraMenos2 && barraMenos2 > barraMenos3 && ordensAbertas <= 0)
                {
                    double price = Symbol.Bid;
                    ConsultarCompra(price, volume , ordMax, tpPips, slPips, ordensCompra);
                }

                if (!valorAcimaMedia && barraMenos1 < barraMenos2 && barraMenos2 < barraMenos3 && ordensAbertas <= 0)
                {
                    double price = Symbol.Ask;
                    ConsultarVenda(price, volume, ordMax, tpPips, slPips, ordensCompra);
                }
            }
        }

        private void ConsultarCompra(double price, double volume, double ordMax, double tpPips, double slPips, int ordensCompra)
        {
            if (ordensCompra < ordMax)
            {
                var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, volume,
                    $"BUY-Loss{contadorLoss}", slPips, tpPips);

                if (result.IsSuccessful)
                {
                    aguardandoFechamento = true;
                    Print($"Ordem de compra executada - Volume: {volume}, SL: {slPips}, TP: {tpPips}");
                }
                else
                {
                    Print($"Erro ao executar ordem de compra: {result.Error}");
                }
            }
        }

        private void ConsultarVenda(double price, double volume, double ordMax, double tpPips, double slPips, int ordensCompra)
        {
            if (ordensCompra < ordMax)
            {
                var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, volume,
                    $"SELL-Loss{contadorLoss}", slPips, tpPips);

                if (result.IsSuccessful)
                {
                    aguardandoFechamento = true;
                    Print($"Ordem de venda executada - Volume: {volume}, SL: {slPips}, TP: {tpPips}");
                }
                else
                {
                    Print($"Erro ao executar ordem de venda: {result.Error}");
                }
            }
        }
    }
}
