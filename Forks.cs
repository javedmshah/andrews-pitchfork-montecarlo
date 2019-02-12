#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class Forks : Strategy
	{
		bool changeHigh = false;
		bool changeMajorLow = false;
		bool changeRecentLow = false;
		bool changeDTmaxHigh = false;
		int counter = 0;
		int dotCounter = 0;
		bool drawFork = false;
		bool downTrend = false;

		double minLowRecent;
		double minLow;
		double maxHigh;
		double dtMaxHigh;
		int dtMaxHighBarsAgo = -1;
		int minLowBarsAgo;
		int minLowRecentBarsAgo;
		int maxHighBarsAgo;
		int medianBarsAgo;
		int medianHBarsAgo;
		int medianLBarsAgo;
		int dtmedianBarsAgo;
		int dtmedianHBarsAgo;
		int dtmedianLBarsAgo;
		
		double prevdtMaxHigh = -1;
		double prevMaxHigh = -1;
		double prevMinLow;
		double prevMinLowRecent;
		int prevMaxHighBarsAgo=-1;
		int prevMedianBarsAgo=-1;
		int prevMinLowBarsAgo=-1;
		int prevMinLowRecentBarsAgo=-1;
		int mediansRecordedBarsAgo=-1;
		double[] mLine;
		double[] hmLine;
		double[] lmLine;
		double[] mlmLine;
		double[] mhmLine;
		double[] ms_mLine;
		double[] ms_hmLine;
		double[] ms_lmLine;
		double[] ms_mlmLine;
		double[] ms_mhmLine;
		double[] dt_mLine;
		double[] dt_hmLine;
		double[] dt_lmLine;
		double[] dt_mlmLine;
		double[] dt_mhmLine;
		
		private Order forkOrder							= null;
		int barCounter;
		int dtbarCounter;
		private Order entryOrder						= null; // This variable holds an object representing our entry order
		private Order stopOrder                         = null; // This variable holds an object representing our stop loss order
		private Order targetOrder                       = null; // This variable holds an object representing our profit target order
		private int barNumberOfOrder 					= 0;	// This variable is used to store the entry bar
		private int priorTradesCount = 0;
		private double priorTradesCumProfit = 0;
		bool modifiedSchiff = false;
		int delta = 0;
		string forkId = "";
		string msforkId = "";
		string dtforkId = "";
		float forkSlope = 0;
		double forkWidth = 0;
		double zeroLine = 0;
		bool dtdrawFork = false;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "Forks";
				Calculate									= Calculate.OnBarClose;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.AdoptAccountPosition;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 10;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= false;
				LIMIT = 3;
				DOTLIMIT = 6;
				WIDTH = 10;
				FORK_LENGTH = 20;
				DT_FORK_LENGTH = 100;
				PROFIT_TARGET = 350;
				FORK_WIDTH = 10;
				HIGH_STRENGTH = 15;
				STOP_VALUE = 550;
				drawPitchforks = true;
				drawModifiedSchiff = true;
				drawDots = true;
				useATRStops = true;
				breakEvenStop = false;
				msfkEntries = true;
				msfkEntriesMlm = true;
				msfkEntriesBrko = true;
				fkEntries = true;
				fkEntriesMlm = true;
				fkEntriesBrko = true;
				fkTargetLine = 1;
				msfkTargetLine = 1;
				BREAKEVEN_TICKS = 20;
				SHOW_BARCOUNT = false;
			}
			else if (State == State.Realtime)
			{
				// one time only, as we transition from historical
			    // convert any old historical order object references
			    // to the new live order submitted to the real-time account
			    if (entryOrder != null)
			        entryOrder = GetRealtimeOrder(entryOrder);
				if (stopOrder != null)
			        stopOrder = GetRealtimeOrder(stopOrder);
				if (targetOrder != null)
			        targetOrder = GetRealtimeOrder(targetOrder);
			}
			else if (State == State.Historical)
			{
				/*Core.Globals.RandomDispatcher.BeginInvoke(new Action(() => 
				{ 
				 	NinjaTrader.Gui.NinjaScript.NinjaScriptOutput newNsOutput = new NinjaTrader.Gui.NinjaScript.NinjaScriptOutput();
					newNsOutput.Show(); 
					newNsOutput.Activate();
				}));*/
			} 
			else if (State == State.DataLoaded)
			{
				mLine = new double[FORK_LENGTH+1];
				mLine[0] = -1;
				hmLine = new double[FORK_LENGTH+1];
				hmLine[0] = -1;
				lmLine = new double[FORK_LENGTH+1];
				lmLine[0] = -1;
				mlmLine = new double[FORK_LENGTH+1];
				mlmLine[0] = -1;
				mhmLine = new double[FORK_LENGTH+1];
				mhmLine[0] = -1;
				ms_mLine = new double[FORK_LENGTH+1];
				ms_mLine[0] = -1;
				ms_hmLine = new double[FORK_LENGTH+1];
				ms_hmLine[0] = -1;
				ms_lmLine = new double[FORK_LENGTH+1];
				ms_lmLine[0] = -1;
				ms_mlmLine = new double[FORK_LENGTH+1];
				ms_mlmLine[0] = -1;
				ms_mhmLine = new double[FORK_LENGTH+1];
				ms_mhmLine[0] = -1;
				dt_mLine = new double[DT_FORK_LENGTH+1];
				dt_mLine[0] = -1;
				dt_hmLine = new double[DT_FORK_LENGTH+1];
				dt_hmLine[0] = -1;
				dt_lmLine = new double[DT_FORK_LENGTH+1];
				dt_lmLine[0] = -1;
				dt_mlmLine = new double[DT_FORK_LENGTH+1];
				dt_mlmLine[0] = -1;
				dt_mhmLine = new double[DT_FORK_LENGTH+1];
				dt_mhmLine[0] = -1;
				
				minLow = 9999;
				maxHigh = 0;
				minLowRecent = 9999;
				minLowBarsAgo = 0;
				minLowRecentBarsAgo = 0;
				maxHighBarsAgo = 0;				
				mediansRecordedBarsAgo = -1;
				barCounter = -99; // no increment if -99, only if 0
				dtbarCounter = -99;
				Draw.TextFixed(this, "x", ""+ (1/TickSize), TextPosition.TopRight);
				Draw.TextFixed(this, "y", ""+ Bars.Instrument.MasterInstrument.PointValue, TextPosition.BottomRight);
			}
		}

		protected override void OnBarUpdate()
		{
			try {
				
				//Add your custom strategy logic here.
				if( CurrentBar < HIGH_STRENGTH*2)
					return;
				if(SHOW_BARCOUNT)
					Draw.Text(this, "cb"+dotCounter, ""+CurrentBar, 0, High[0]);
				//dotCounter++;
				//if(ToTime(Time[0]) < 95600 && ToTime(Time[0]) > 124500) {
				//	return;
				//}
				// At the start of a new session
				if (Bars.IsFirstBarOfSession)
				{
					// Store the strategy's prior cumulated realized profit and number of trades
					priorTradesCount = SystemPerformance.AllTrades.Count;
					priorTradesCumProfit = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;

					/* NOTE: Using .AllTrades will include both historical virtual trades as well as real-time trades.
					If you want to only count profits from real-time trades please use .RealtimeTrades. */
				}
				// cancel pending orders at session close
				if(Bars.IsLastBarOfSession) {
					if(entryOrder != null)
					{
						Draw.ArrowDown(this, "o"+dotCounter, true, 0, Close[0], Brushes.Red);
						Draw.Text(this, "cl"+dotCounter, ""+entryOrder.Name, 0, Low[0], Brushes.Red);
	                    dotCounter++;
						CancelOrder(entryOrder);
						entryOrder = null;						
					}
				}
				
				
				// 
				if(barCounter != -99)
					barCounter++;
				if(dtbarCounter != -99)
					dtbarCounter++;
				if(barCounter > FORK_LENGTH) {
					// need to draw a new one 
					prevMinLowBarsAgo = -1;
					prevMinLowRecentBarsAgo = -1;
					maxHigh = -1;
					prevMaxHigh = -1;
					maxHighBarsAgo = -1;
					prevMaxHighBarsAgo = -1;
					minLowRecentBarsAgo = -1;
					mLine[0] = -1;
					lmLine[0] = -1;
					hmLine[0] = -1;
					mhmLine[0] = -1;
					mlmLine[0] = -1;
					barCounter = -99;
					changeMajorLow = false;
					changeHigh = false;
					changeRecentLow = false;
				}
				if(dtbarCounter > DT_FORK_LENGTH) {
					dtMaxHigh = -1;
					dt_mLine[0] = -1;
					dt_lmLine[0] = -1;
					dt_hmLine[0] = -1;
					dt_mhmLine[0] = -1;
					dt_mlmLine[0] = -1;
					dtbarCounter = -99;
					changeDTmaxHigh = false;
				}
				double atr = Math.Round(ATR(WIDTH)[0]);
				
				// get me the highest high since a N prior high from total HIGH_STRENGTH bars ago
				prevMaxHighBarsAgo = maxHighBarsAgo;
				prevMaxHigh = maxHigh;
				maxHigh = High[HighestBar(High,Math.Max(prevMaxHighBarsAgo, HIGH_STRENGTH))]; 
				// lets find how many bars ago
				for(int i=0; i<Math.Max(prevMaxHighBarsAgo, HIGH_STRENGTH); i++) {
					if(maxHigh == High[i]) {
						maxHighBarsAgo = i;
					}
				}
				
				
				// ensure this is a different value
				if(prevMaxHigh != maxHigh) {
					//if(barCounter != -1 && maxHigh > lmLine[0]) {
						changeHigh = true;
						if(drawDots) 
							Draw.Dot(this, "dot"+dotCounter, true, (int) maxHighBarsAgo, maxHigh, Brushes.Green);
						
					//}
				}

				// get me the lowest low since the prior low from total 20 bars ago
				// start the computation from the bar number we recorded a major high above
				//if(prevMinLow != minLow)
					prevMinLow = minLow;
				//if(prevMinLowBarsAgo != minLowBarsAgo)
					prevMinLowBarsAgo = minLowBarsAgo;
				// find the bars ago for this major low candidate
				minLow = Low[maxHighBarsAgo+1];
				minLowBarsAgo = maxHighBarsAgo+1;
				for(int i=minLowBarsAgo; i<maxHighBarsAgo+HIGH_STRENGTH; i++) {				
					if (minLow >= Low[i]) {
						minLow = Low[i];
						minLowBarsAgo = i;
					}
				}
				// only makes sense to get a major low if major high changed, right?
				if(changeHigh) {
					// this is a different value
					if(minLow != prevMinLow ) {	
						//if(barCounter != -1 && minLow < lmLine[0]) {
							changeMajorLow = true;
							if(drawDots) 
								Draw.Dot(this, "dml"+dotCounter, true, minLowBarsAgo, minLow, Brushes.Red);
							
						//}
					}
				}
				double mlSlope = -1;
				double dtmid = -1;
				double mid = -1;
				double dY = -1;
				double dX = -1;
				if(drawDTPitchforks) {
				
					//############# check for DT pitchforks #################
					// need a previous major high from this new major high
					// and need a major low in between, simple.
					// get me the highest high that occured BEFORE the minLow
					prevdtMaxHigh = dtMaxHigh;
					dtMaxHigh = High[minLowBarsAgo+1];
					dtMaxHighBarsAgo = minLowBarsAgo + 1;
					for(int i=dtMaxHighBarsAgo; i<minLowBarsAgo+2*HIGH_STRENGTH; i++) {				
						if (dtMaxHigh <= High[i]) {
							dtMaxHigh = High[i];
							dtMaxHighBarsAgo = i;
						}
					}
					
					if(changeMajorLow) {
						changeDTmaxHigh = true;
						//if(drawDots) 
							Draw.Dot(this, "dtml"+dotCounter, true, dtMaxHighBarsAgo, dtMaxHigh, Brushes.DarkGray);
					}
					
					if(changeHigh && changeMajorLow && changeDTmaxHigh) {	
						dtmid = (maxHigh + minLow)/2;
						dtmedianBarsAgo = (maxHighBarsAgo + minLowBarsAgo)/2;
						dtmedianLBarsAgo = (maxHighBarsAgo + dtmedianBarsAgo)/2;
						dtmedianHBarsAgo = (dtmedianBarsAgo + minLowBarsAgo)/2;
						
						dY = dtmid - dtMaxHigh;
						dX = Math.Abs(dtmedianBarsAgo - dtMaxHighBarsAgo);
						mlSlope = dY/dX;
						dtdrawFork = false;
						
						// make sure high is between two lows
						if(mlSlope < 0 
							// check width of fork
							&& (maxHigh - minLow) >= 2*FORK_WIDTH 
							&& minLowBarsAgo > maxHighBarsAgo && dtMaxHighBarsAgo > minLowBarsAgo
							)
						{				
							dtdrawFork = true;
						}
						if(dt_lmLine[0] != -1 && dtbarCounter > 0) {
							// so we have a previous DT fork to compare with
							// make sure our points A and B are outside of the previous fork
							if (dtMaxHigh > dt_lmLine[dtbarCounter]) {
								
							} else {
								dtdrawFork = false;
							}
						}

						if(dtdrawFork) {					
							dt_mLine[0] = dtmid;
							dt_lmLine[0] = maxHigh;
							dt_mlmLine[0] = (maxHigh + dtmid)/2;
							dt_hmLine[0] = minLow;
							dt_mhmLine[0] = (minLow + dtmid)/2;
							
							for(int i=0; i<DT_FORK_LENGTH; i++) {
								dt_mLine[i] = dt_mLine[0] + mlSlope*(i + dtmedianBarsAgo);
							}
							
							for(int i=0; i<DT_FORK_LENGTH; i++) {
								dt_mhmLine[i] = dt_mhmLine[0] + mlSlope*(i + dtmedianHBarsAgo);
							}
											
							for(int i=0; i<DT_FORK_LENGTH; i++) {
								dt_hmLine[i] = dt_hmLine[0] + mlSlope*(i + maxHighBarsAgo);
							}
							
							for(int i=1; i<DT_FORK_LENGTH; i++) {
								dt_lmLine[i] = dt_lmLine[0] + mlSlope*(i);
							}
							
							for(int i=0; i<DT_FORK_LENGTH; i++) {
								dt_mlmLine[i] = dt_mlmLine[0] + mlSlope*(i + dtmedianLBarsAgo);
							}
							dtbarCounter = 0;
							dtforkId = "_dtfk:"+Math.Round(mlSlope, 2)+"-"+Math.Round(atr, 2);
							
						    // just draw triangles
							if(drawDTPitchforks) {
								dtbarCounter = 0;
		                        Draw.Line(this, "dtab"+counter, dtMaxHighBarsAgo, dtMaxHigh, minLowBarsAgo, minLow, Brushes.DimGray);
								Draw.Line(this, "dtbc"+counter, minLowBarsAgo, minLow, maxHighBarsAgo, maxHigh, Brushes.DimGray);
								Draw.Line(this, "dtac"+counter, dtMaxHighBarsAgo, dtMaxHigh, maxHighBarsAgo, maxHigh, Brushes.DimGray);
								Draw.Text(this, "id"+counter, forkId, 
									(dtMaxHighBarsAgo+maxHighBarsAgo)/2, (dtMaxHigh+maxHigh)/2, Brushes.DarkTurquoise);
							} 
						}
						
					}
					
				}
				// ########### End DT fork calcs ##################
				
				// get me the lowest low since the maxHigh occurred
				//if(prevMinLowRecent != minLowRecent)
					prevMinLowRecent = minLowRecent;
				//if(prevMinLowRecentBarsAgo != minLowRecent)
					prevMinLowRecentBarsAgo = minLowRecentBarsAgo;
					
				minLowRecent = Low[LowestBar(Low, maxHighBarsAgo-1)];
				// now find the bars ago this recent low occurred after the maxHigh
				for(int i=0; i<(int)maxHighBarsAgo-1; i++) {	
					if(minLowRecent == Low[i]) {
						minLowRecentBarsAgo = i;
					}
				}
				if(prevMinLowRecent != minLowRecent) {
					changeRecentLow = true;
				}
				
				
				downTrend = (Math.Abs(minLowRecent - minLow) > (maxHigh - minLow)) ? true : false;
				
				// only use this low if it makes for a good fork
				if(//changeHigh && changeMajorLow				
					// dont draw steep uptrend forks
					//&& (minLowRecent <= (maxHigh+minLow)/2)
					// dont allow recent low higher than maxHigh
					//&& minLowRecent < maxHigh
					// check width of fork
					(maxHigh - minLowRecent) >= FORK_WIDTH //2*atr
					// dont draw down trend forks
					&& !downTrend) {
					
						changeRecentLow = true;
						downTrend = false;
						
				} else {
					changeRecentLow = false;
				}
				
				
				if(changeHigh && changeMajorLow && changeRecentLow) {	
					mid = (maxHigh + minLowRecent)/2;
					medianBarsAgo = (maxHighBarsAgo + minLowRecentBarsAgo)/2;
					medianHBarsAgo = (maxHighBarsAgo + medianBarsAgo)/2;
					medianLBarsAgo = (medianBarsAgo + minLowRecentBarsAgo)/2;
					dY = mid - minLow;
					dX = Math.Abs(medianBarsAgo - minLowBarsAgo);
					mlSlope = dY/dX;
					drawFork = false;
					// make sure high is between two lows
					if(mlSlope > 0 
						&& minLowBarsAgo > maxHighBarsAgo && minLowRecentBarsAgo < maxHighBarsAgo)
					{				
						drawFork = true;
					}
					if(barCounter >= 1 && barCounter < FORK_LENGTH) {	
						if(minLowRecent > lmLine[barCounter]) {
					   		drawFork = false;						
						} else {
							modifiedSchiff = false;
						}
					}
				}
				
				if(drawFork) {
					mLine[0] = mid;
					hmLine[0] = maxHigh;
					mhmLine[0] = (maxHigh + mid)/2;
					lmLine[0] = minLowRecent;
					mlmLine[0] = (minLowRecent + mid)/2;
					forkSlope = (float) mlSlope;
					
					for(int i=0; i<FORK_LENGTH; i++) {
						mLine[i] = mLine[0] + mlSlope*(i + medianBarsAgo);
					}
					
					for(int i=0; i<FORK_LENGTH; i++) {
						mhmLine[i] = mhmLine[0] + mlSlope*(i + medianHBarsAgo);
					}
									
					for(int i=0; i<FORK_LENGTH; i++) {
						hmLine[i] = hmLine[0] + mlSlope*(i + maxHighBarsAgo);
					}
					
					for(int i=1; i<FORK_LENGTH; i++) {
						lmLine[i] = lmLine[0] + mlSlope*(i);
					}
					
					for(int i=0; i<FORK_LENGTH; i++) {
						mlmLine[i] = mlmLine[0] + mlSlope*(i + medianLBarsAgo);
					}
					
					//Draw.Dot(this, "dh"+dotCounter, true, (int) maxHighBarsAgo, maxHigh, Brushes.Green);
                    //Draw.Dot(this, "dml"+dotCounter, true, (int)minLowBarsAgo, minLow, Brushes.Red);
                    //Draw.Dot(this, "dmlr"+dotCounter, true, (int)minLowRecentBarsAgo, minLowRecent-atr, Brushes.Pink);
                    //dotCounter++;
					barCounter = 0;
					forkId = "_fk:"+Math.Round(mlSlope, 2)+"-"+Math.Round(atr, 2);
					forkWidth = hmLine[0] - lmLine[0];
				    // just draw triangles
					if(drawPitchforks) {
						barCounter = 0;
                        Draw.Line(this, "ab"+counter, minLowBarsAgo, minLow, maxHighBarsAgo, maxHigh, Brushes.Blue);
						Draw.Line(this, "bc"+counter, maxHighBarsAgo, maxHigh, minLowRecentBarsAgo, minLowRecent, Brushes.Red);
						Draw.Line(this, "ac"+counter, minLowBarsAgo, minLow, minLowRecentBarsAgo, minLowRecent, Brushes.Black);
						Draw.Text(this, "id"+counter, forkId, 
							(int) (minLowBarsAgo+minLowRecentBarsAgo)/2, (minLow+minLowRecent)/2, Brushes.MediumSeaGreen);
					} 
					
					// calculate the modified schiff anyway
					//####################################################
					
					// only makes sense to add MHML and MLML lines 
					// if maxHighBA - minLowRecent was at least = 2
					
					mid = minLowRecent;
					// overwrite minLowRecent
					minLowRecent = minLowRecent - (maxHigh - minLowRecent);
					delta = maxHighBarsAgo - minLowRecentBarsAgo;	
					int msfkMedianBarsAgo = minLowRecentBarsAgo;
					int msfkMaxHighBarsAgo = maxHighBarsAgo;
						
					// the new median
					medianBarsAgo = minLowRecentBarsAgo;
					
					//CHECK IF SHIFTING IS NEEDED
					int Db = medianBarsAgo - (maxHighBarsAgo - medianBarsAgo);
					if (Db < 0) {
						// now adjust
						maxHighBarsAgo += Math.Abs(Db);
						medianBarsAgo += Math.Abs(Db);
						minLowRecentBarsAgo = 0;
					} 
					barCounter = Db;
					if(delta >= 2) 
						medianHBarsAgo = (maxHighBarsAgo + medianBarsAgo)/2;
					if(Db >=0 )
						minLowRecentBarsAgo = Db;
					if(delta >= 2) {
						if (medianHBarsAgo % 2 == 1)
							medianHBarsAgo += 1;				// set mhml closer to hml
					}
					if(delta >= 2) {
						medianLBarsAgo = (medianBarsAgo)/2;	
						if (medianLBarsAgo % 2 == 1)
							medianLBarsAgo -= 1;				// set mlml closer to lml
					}
						
					dY = mid - minLow;
					dX = Math.Abs(medianBarsAgo - minLowBarsAgo);
					mlSlope = dY/dX;
					if(mlSlope < 0 ) {
						drawFork = false;
						modifiedSchiff = false;
						delta = 0;
					} else if(mlSlope > 0) {
						modifiedSchiff = true;						
						ms_mLine[0] = mid;
						ms_hmLine[0] = maxHigh;
						if (delta >= 2)
							ms_mhmLine[0] = (maxHigh + mid)/2;
						else
							ms_mhmLine[0] = -1;
						ms_lmLine[0] = minLowRecent;
						if (delta >= 2)
							ms_mlmLine[0] = (minLowRecent + mid)/2;
						else
							ms_mlmLine[0] = -1;
						
						forkSlope = (float) mlSlope;
						
						for(int i=0; i<FORK_LENGTH; i++) {
							ms_mLine[i] = ms_mLine[0] + mlSlope*(i + medianBarsAgo);
						}
						
						if (delta >= 2)
							for(int i=0; i<FORK_LENGTH; i++) {
								ms_mhmLine[i] = ms_mhmLine[0] + mlSlope*(i + medianHBarsAgo);
							}
										
						for(int i=0; i<FORK_LENGTH; i++) {
							ms_hmLine[i] = ms_hmLine[0] + mlSlope*(i + maxHighBarsAgo);
						}
						
						for(int i=1; i<FORK_LENGTH; i++) {
							ms_lmLine[i] = ms_lmLine[0] + mlSlope*(i);
						}
						
						if (delta >= 2)
							for(int i=0; i<FORK_LENGTH; i++) {
								ms_mlmLine[i] = ms_mlmLine[0] + mlSlope*(i + medianLBarsAgo);
							}
						
	                	msforkId = "msfk:"+Math.Round(mlSlope, 2)+"-"+Math.Round(atr, 2);
						int msfkLowRecentBA = -1;
						if( Db < 0 )
							msfkLowRecentBA = Db;
						else
							msfkLowRecentBA = minLowRecentBarsAgo;
						// pls adjust the bars back..we will need it for DT calcs
						if (Db < 0) {
							// now adjust
							maxHighBarsAgo -= Math.Abs(Db);
							medianBarsAgo -= Math.Abs(Db);
						} 
							
						if(drawFork && drawModifiedSchiff) {
							Draw.Dot(this, "schiff"+dotCounter, true, msfkMedianBarsAgo, Math.Round(mid), Brushes.Yellow);
							dotCounter++;
							Draw.Line(this, "msab"+counter, minLowBarsAgo, minLow, msfkMaxHighBarsAgo, maxHigh, Brushes.Blue);
							Draw.Line(this, "msbc"+counter, msfkMaxHighBarsAgo, maxHigh, msfkLowRecentBA, minLowRecent, Brushes.Red);
							Draw.Line(this, "msac"+counter, minLowBarsAgo, minLow, msfkLowRecentBA, minLowRecent, Brushes.Black);
							Draw.Text(this, "msid"+counter, msforkId, 
								(int) (minLowBarsAgo+minLowRecentBarsAgo)/2, (minLow+minLowRecent)/2, Brushes.YellowGreen);
						}
					}
						
					//####################################################
					//Draw.Text(this, "slp"+counter, ""+mlSlope, (int)maxHighBarsAgo, maxHigh+2);
					counter++;
					// if we drew a new fork and have a current position, exit pls
					//if (Position.MarketPosition == MarketPosition.Long) {
					//	ExitLong("touch");
					//	entryOrder = null;
					//}
					// if we have an as yet unfilled limit order from a previous fork, cancel it
					if(entryOrder != null && drawFork) //(CurrentBar - barNumberOfOrder) >= (int) atr)) 
					{
						Draw.ArrowDown(this, "o"+dotCounter, true, 0, Close[0], Brushes.Red);
						Draw.Text(this, "cl"+dotCounter, ""+entryOrder.Name, 0, Low[0], Brushes.Red);
                        dotCounter++;
						CancelOrder(entryOrder);
						entryOrder = null;						
					}					
					//
					drawFork = false;
					changeHigh = changeMajorLow = changeRecentLow = false;
				
	  			}
					
	            // ##################################################
	            // ################## check for entries #############
				
				double limitOrderPriceL = -1;
				string signalName = "";
				if(msforkId.IndexOf("msfk") != -1 && msfkEntries
					&& barCounter >= 1 && barCounter < FORK_LENGTH - 1) { 
					// ENTRY modified schiff lmLine barrier
					if(Low[0] < ms_lmLine[barCounter]
						&& Close[0] > ms_lmLine[barCounter] + TickSize
						&& High[1] > ms_lmLine[barCounter] 
						&& High[2] > ms_lmLine[barCounter]
						) {
						if(entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
							barNumberOfOrder = CurrentBar;
							Draw.ArrowUp(this, "mso"+dotCounter, true, 0, ms_lmLine[barCounter], Brushes.Yellow);
							Draw.Text(this, "msfk"+dotCounter, " ms_lm", 1, ms_lmLine[barCounter], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, ""+Math.Round(ms_lmLine[barCounter], 2), 0, ms_lmLine[barCounter], Brushes.Olive);
							limitOrderPriceL = ms_lmLine[barCounter];
							signalName = msforkId;
							zeroLine = ms_lmLine[0];
						}
					}
				}
				if(msforkId.IndexOf("msfk") != -1  && msfkEntriesMlm && mlmLine[0] != -1
					&& barCounter >= 1 && barCounter < FORK_LENGTH - 1) {
					//ENTRY mlmLine barrier
					if(Low[0] <= ms_mlmLine[barCounter] && Low[0] > ms_lmLine[barCounter]
						&& High[0] <= ms_mhmLine[barCounter]
						&& Close[0] > ms_mlmLine[barCounter] + TickSize
						&& High[1] > ms_mlmLine[barCounter] 
						&& High[2] > ms_mlmLine[barCounter]
						) {
						if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
	                        barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "mso"+dotCounter, true, 0, ms_mlmLine[barCounter], Brushes.Olive);
							Draw.Text(this, "msfk"+dotCounter, " ms_mlm", 1, ms_mlmLine[barCounter], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, "long: "+Math.Round(ms_mlmLine[barCounter], 2), 0, ms_mlmLine[barCounter], Brushes.Olive);
							limitOrderPriceL = ms_mlmLine[barCounter];
							signalName = msforkId;
							zeroLine = ms_lmLine[0];
	                    }
					}
				}
				if (forkId.IndexOf("_fk") != -1  && fkEntries
					&& barCounter >= 1 && barCounter < FORK_LENGTH - 1) {
	                // ENTRY pitchfork lmLine barrier
	                if (Low[0] <= lmLine[barCounter]
	                    && Close[0] > lmLine[barCounter] + TickSize
						&& High[1] > lmLine[barCounter] 
						&& High[2] > lmLine[barCounter]
	                    ) {
	                    if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
							barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "o" + dotCounter, true, 0, lmLine[barCounter], Brushes.Green);
							Draw.Text(this, "fk"+dotCounter, " lml", 1, lmLine[barCounter], Brushes.Olive);
	                        dotCounter++;
							limitOrderPriceL = lmLine[barCounter]+TickSize;
							signalName = forkId;
							zeroLine = lmLine[0];
	                    }
	                }
	            }
	            
				if(forkId.IndexOf("_fk") != -1  && fkEntriesMlm //&& forkWidth >= FORK_WIDTH
					&& barCounter >= 1 && barCounter < FORK_LENGTH - 1) {
					//ENTRY mlmLine barrier
					if(Low[0] <= mlmLine[barCounter] && Low[0] > lmLine[barCounter]
						&& High[0] <= mhmLine[barCounter]
						&& Close[0] > mlmLine[barCounter] + TickSize
						&& High[1] > mlmLine[barCounter] 
						&& High[2] > mlmLine[barCounter]
						) {
						if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
	                        barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "o"+dotCounter, true, 0, mlmLine[barCounter], Brushes.Olive);
							Draw.Text(this, "fk"+dotCounter, " fk_mlm", 1, mlmLine[barCounter], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, "long: "+Math.Round(mlmLine[barCounter], 2), 0, mlmLine[barCounter], Brushes.Olive);
	                      	limitOrderPriceL = mlmLine[barCounter];
							signalName = forkId;
							zeroLine = lmLine[0];
	                    }
					}
				}
				// lower breakouts
				if( msforkId.IndexOf("msfk") != -1 && msfkEntriesBrko
					&& barCounter >= 2 && barCounter < FORK_LENGTH - 1) {
					//ENTRY breakout at lmLine
					if(High[2] <= ms_lmLine[barCounter]
						&& High[1] <= ms_lmLine[barCounter]
						&& High[0] > ms_lmLine[barCounter]
						&& Close[0] < ms_lmLine[barCounter]
						) {
						if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
	                        barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "mso"+dotCounter, true, 0, ms_lmLine[barCounter], Brushes.Olive);
							Draw.Text(this, "msk"+dotCounter, " ms_brko", 1, High[0], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, ""+Math.Round(ms_lmLine[barCounter], 2), 0, ms_lmLine[barCounter], Brushes.Olive);
							limitOrderPriceL = ms_lmLine[barCounter] + TickSize;
							signalName = msforkId;
							zeroLine = ms_lmLine[0];
	                    }
					}
				}
				// lower breakouts
				if( forkId.IndexOf("_fk") != -1  && fkEntriesBrko
					&& barCounter >= 2 && barCounter < FORK_LENGTH - 1) {
					//ENTRY breakout at lmLine
					if(High[2] <= lmLine[barCounter]
						&& High[1] <= lmLine[barCounter]
						&& High[0] > lmLine[barCounter]
						&& Close[0] < lmLine[barCounter]
						) {
						if (entryOrder == null && Position.MarketPosition == MarketPosition.Flat) {
	                        barNumberOfOrder = CurrentBar;
	                        Draw.ArrowUp(this, "o"+dotCounter, true, 0, lmLine[barCounter], Brushes.Olive);
							Draw.Text(this, "k"+dotCounter, " fk_brko", 1, High[0], Brushes.Olive);
							//Draw.Text(this, "bc"+dotCounter, ""+Math.Round(lmLine[barCounter], 2), 0, lmLine[barCounter], Brushes.Olive);
							limitOrderPriceL = lmLine[barCounter] + TickSize;
							signalName = forkId;							
							zeroLine = lmLine[0];
	                    }
					}
				}
				//  as long as the stopPrice is not more than the limitPrice. So, in your case, if x >= y, the order will not be rejected. 
				if (limitOrderPriceL != -1) {
					if (GetCurrentAsk() < limitOrderPriceL) 
						EnterLongStopLimit(0, true, 1, limitOrderPriceL, limitOrderPriceL - TickSize, signalName);
					else 
						EnterLongLimit(0, true, 1, limitOrderPriceL, signalName);
					limitOrderPriceL = -1;	
				}
				
				// #################################################

				// exit positions that dont match this fork
				if (Position.MarketPosition == MarketPosition.Long) {
					// cancel pending unfilled orders
					if(entryOrder!=null) {
						if(entryOrder.Name != forkId && entryOrder.Name != msforkId) {
							Draw.ArrowDown(this, "o"+dotCounter, true, 0, Close[0], Brushes.Red);
							Draw.Text(this, "cl"+dotCounter, ""+entryOrder.Name, 0, Low[0], Brushes.Red);
							CancelOrder(entryOrder);
							entryOrder = null;		
						}					
					}
					// exit filled orders
					if(stopOrder!=null) {
						if(stopOrder.FromEntrySignal != forkId && stopOrder.FromEntrySignal != msforkId) {
							//SetTrailStop(stopOrder.FromEntrySignal, CalculationMode.Price, Position.AveragePrice, false);
							ExitLong("difk", stopOrder.FromEntrySignal);
						}
					}
				}
				// ############################################
				// breakeven stop if some ticks in profit
				if (breakEvenStop && Position.MarketPosition == MarketPosition.Long 
					&& Close[0] >= Position.AveragePrice - (BREAKEVEN_TICKS*2 * (TickSize / 2)))
				{
					// Checks to see if our Stop Order has been submitted already
					if (stopOrder != null && stopOrder.StopPrice < Position.AveragePrice)
					{
						//SetTrailStop(CalculationMode.Ticks, 20);
						// Modifies stop-loss to breakeven
						stopOrder = ExitLongStopMarket(
							0, true, stopOrder.Quantity, 
							Position.AveragePrice + TickSize, "chkn", forkOrder.Name);
					}
				}
				
				dotCounter++;
				// only draw N pitchforks at any given time
				if(counter >= LIMIT) counter = 0;
				if(dotCounter >= DOTLIMIT) dotCounter = 0;
			} catch(Exception ex) {
				Print(ex.StackTrace);
			}
		}
		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
		{
			if (entryOrder == null && (order.Name == forkId || order.Name == msforkId) )
    		{	
        		entryOrder = order;
				forkOrder = order;

                // Reset the entryOrder object to null if order was cancelled without any fill
                if (order.OrderState == OrderState.Cancelled && order.Filled == 0)
					entryOrder = null;
			}
		}

		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
		{
			try {
				double atrLocal = Math.Round(ATR(WIDTH)[0]);
				double atrDollarValue =  atrLocal * Bars.Instrument.MasterInstrument.PointValue;
				// ##########################################################################
				/* We advise monitoring OnExecution to trigger submission of stop/target orders instead of OnOrderUpdate() since OnExecution() is called after OnOrderUpdate()
				which ensures your strategy has received the execution which is used for internal signal tracking. */
				if (entryOrder != null && entryOrder == execution.Order && barCounter >=0 )
				{
					if (execution.Order.OrderState == OrderState.Filled 
						|| execution.Order.OrderState == OrderState.PartFilled 
						|| (execution.Order.OrderState == OrderState.Cancelled && execution.Order.Filled > 0))
					{
						
							// adjust stop based on fill price
							// tight stop
							int myStop = STOP_VALUE;
							double pointTicks = 1/TickSize; // 4 for ES
							if(useATRStops) {
								double gap = (execution.Order.AverageFillPrice - zeroLine);
							
								gap = gap * Bars.Instrument.MasterInstrument.PointValue; // times 50 for ES
								if (gap < STOP_VALUE && atrDollarValue < STOP_VALUE) {
									// gap + 1 point stop
									myStop = (int) (gap + Bars.Instrument.MasterInstrument.PointValue); 
									// if stop less than atr*pointvalue, then atr stop
									myStop = myStop < atrDollarValue ? (int) atrDollarValue : myStop;
								} else {
									// no change to STOP setting
									myStop = STOP_VALUE;
								}
							}
							Draw.Text(this, "stp"+dotCounter, "-$"+ myStop, 0, High[0]+2*TickSize);
							Draw.Text(this, "tgt"+dotCounter, 
								""+ Math.Min(Position.AveragePrice + myStop/Bars.Instrument.MasterInstrument.PointValue, 
								Math.Round(mhmLine[barCounter])), 0, High[0]);
							dotCounter++;
							
							stopOrder = ExitLongStopMarket(
								0, true, execution.Order.Filled, 
								execution.Order.AverageFillPrice - (myStop/Bars.Instrument.MasterInstrument.PointValue), 
								"atrStop", forkOrder.Name);
							
							double pt = Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue;
							
							if(forkOrder.Name.IndexOf("msfk") != -1) {
								switch(msfkTargetLine) {
								case 1:
									pt = Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
									Math.Round(ms_mLine[barCounter]));
									break;
								case 2:
									if(ms_mhmLine[0] != -1)
										pt = Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
											Math.Round(ms_mhmLine[barCounter]));
									else
										pt = Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
											Math.Round(ms_hmLine[barCounter]));
									break;
								case 3:
									pt = Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
										Math.Round(ms_hmLine[barCounter]));
									break;
								}
								targetOrder = ExitLongLimit(
									0, true, execution.Order.Filled,
									//Math.Max(Position.AveragePrice +  forkWidth/2, Position.AveragePrice + atrLocal),
									//Math.Min(Position.AveragePrice +  48/pointTicks, Math.Round(hmLine[barCounter])),
									//Math.Round(hmLine[barCounter]) -2*pointTicks,
									pt,
									"msfk", forkOrder.Name);
							}
							if(forkOrder.Name.IndexOf("_fk") != -1) {
								switch(fkTargetLine) {
								case 1:
									pt = Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
											Math.Round(mLine[barCounter]));
									break;
								case 2:
									pt = Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
											Math.Round(mhmLine[barCounter]));
									break;
								case 3:
									pt = Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
										Math.Round(hmLine[barCounter]));
									break;
								}
								targetOrder = ExitLongLimit(
									0, true, execution.Order.Filled,
									//Math.Max(Position.AveragePrice +  forkWidth/2, Position.AveragePrice + atrLocal),
									//Math.Round(hmLine[barCounter]) -2*pointTicks,
									Math.Min(Position.AveragePrice + PROFIT_TARGET/Bars.Instrument.MasterInstrument.PointValue, 
									Math.Round(mLine[barCounter])),
									"_fk", forkOrder.Name);
							}
						

						// Resets the entryOrder object to null after the order has been filled
						if (execution.Order.OrderState != OrderState.PartFilled)
							entryOrder = null;
					}
				}

				// Reset our stop order and target orders' Order objects after our position is closed.
				if ((stopOrder != null && stopOrder == execution.Order) || (targetOrder != null && targetOrder == execution.Order))
				{
					if (execution.Order.OrderState == OrderState.Filled || execution.Order.OrderState == OrderState.PartFilled)
					{
						stopOrder = null;
						targetOrder = null;
					}
				}
			} catch(Exception ex) {
				Print(ex.StackTrace);
			}
		}
		protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
		{
			// Print our current position to the lower right hand corner of the chart
			//Draw.TextFixed(this, "MyTag", ""+position., TextPosition.BottomRight, false, "");
		}
		#region Properties
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "LIMIT", GroupName = "NinjaScriptParameters", Order = 0)]
		public int LIMIT 
		{ get; set; }
		
		[Range(1, 30), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "WIDTH", GroupName = "NinjaScriptParameters", Order = 1)]
		public int WIDTH 
		{ get; set; }
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DOTLIMIT", GroupName = "NinjaScriptParameters", Order = 2)]
		public int DOTLIMIT 
		{ get; set; }
		
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FORK_LENGTH", GroupName = "NinjaScriptParameters", Order = 3)]
		public int FORK_LENGTH 			
		{ get; set; }
		
		[Range(0, float.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FORK_WIDTH", GroupName = "NinjaScriptParameters", Order = 4)]
		public float FORK_WIDTH	
		{ get; set; }
		
		[Range(1, 300), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "HIGH_STRENGTH", GroupName = "NinjaScriptParameters", Order = 5)]
		public int HIGH_STRENGTH
		{ get; set; }
		
		[Range(100, 3000), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "STOP_VALUE", GroupName = "NinjaScriptParameters", Order = 6)]
		public int STOP_VALUE
		{ get; set; }
		
		
		[Range(50, 3000), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "PROFIT_TARGET", GroupName = "NinjaScriptParameters", Order =7)]
		public int PROFIT_TARGET
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DRAW_DOTS", GroupName = "NinjaScriptParameters", Order =8)]
		public bool drawDots
		{ get; set; }
		
	
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "USE_ATR_STOPS", GroupName = "NinjaScriptParameters", Order = 10)]
		public bool useATRStops
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "BREAKEVEN_STOP", GroupName = "NinjaScriptParameters", Order = 11)]
		public bool breakEvenStop
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DRAW_PITCHFORKS", GroupName = "NinjaScriptParameters", Order = 12)]
		public bool drawPitchforks
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DRAW_MODIFIED_SCHIFF", GroupName = "NinjaScriptParameters", Order = 13)]
		public bool drawModifiedSchiff
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "MSFK_LM_ENTRIES", GroupName = "NinjaScriptParameters", Order = 14)]
		public bool msfkEntries
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "MSFK_MLM_ENTRIES", GroupName = "NinjaScriptParameters", Order = 15)]
		public bool msfkEntriesMlm
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "MSFK_BRKO_ENTRIES", GroupName = "NinjaScriptParameters", Order = 16)]
		public bool msfkEntriesBrko
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FK_LM_ENTRIES", GroupName = "NinjaScriptParameters", Order = 17)]
		public bool fkEntries
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FK_MLM_ENTRIES", GroupName = "NinjaScriptParameters", Order = 18)]
		public bool fkEntriesMlm
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FK_BRKO_ENTRIES", GroupName = "NinjaScriptParameters", Order = 19)]
		public bool fkEntriesBrko
		{ get; set; }
		[Range(1, 5), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "FK_TARGET_LINE", GroupName = "NinjaScriptParameters", Order = 20)]
		public int fkTargetLine
		{ get; set; }
		[Range(1, 5), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "MSFK_TARGET_LINE", GroupName = "NinjaScriptParameters", Order = 21)]
		public int msfkTargetLine
		{ get; set; }
		[Range(5, 100), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "BREAKEVEN_TICKS", GroupName = "NinjaScriptParameters", Order = 22)]
		public int BREAKEVEN_TICKS
		{ get; set; }
		[Range(0, 100), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DT_FORK_LENGTH", GroupName = "NinjaScriptParameters", Order = 23)]
		public int DT_FORK_LENGTH
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "DRAW_DT_PITCHFORKS", GroupName = "NinjaScriptParameters", Order = 24)]
		public bool drawDTPitchforks
		{ get; set; }
		[NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "SHOW_BARCOUNT", GroupName = "NinjaScriptParameters", Order = 25)]
		public bool SHOW_BARCOUNT
		{ get; set; }
		#endregion
	}
}