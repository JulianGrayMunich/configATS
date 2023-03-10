using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics.Metrics;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shapes;
using System.Xml.Linq;

using Microsoft.Win32;

#pragma warning disable IDE0059
#pragma warning disable IDE1006
#pragma warning disable CS8600


namespace configATS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 


    public partial class MainWindow : Window
    {
        //private ObservableCollection<PrismCoords> prismCoords;

        public MainWindow()
        {
            InitializeComponent();
            List<PrismCoords> prismCoords = new();
            List<ATSCoords> atsCoords = new();
            List<OrientationObservations> orientationObs = new();
            List<TransCoords> transCoords = new();
        }

        // Reading the cells in the datagrid
        //foreach (var item in dgPrismCoords.Items.OfType<PrismCoords>())
        //{
        //      var name = item.Name;
        //      var height = item.H;
        //      MessageBox.Show(name + ": " + height);
        //}



        private void btnCompute_Click(object sender, RoutedEventArgs e)
        {
            // Read the prism coordinates
            // read the gka observations

            double dblGonToRad = 0.0157079633;

            double NP;
            double EP;
            double dblUpdatedOrrCorr = 0.0;

            int iIterationCounter = 0;
            

            List<ATSCoords> atsCoords = new();
            List<PrismCoords> prismCoords = new();
            List<OrientationObservations> orientationObs = new();

            // Repeat until (ATSnew - ATS < 0,001)
            //      compute the orientation correction
            //      compute the coordinates from the gka observations + orientation correction
            //      compute the transformation parameters
            //      compute the ATSnew coordinate
            // if ATSnew - ATS > 0,001 repeat
            // Compute the ATS(height)
            // compute the residuals
            // populate the dgResults DataGrid

            

            // read the ATS coordinate
            string strATSname = tbATS_name.Text;
            double dblATS_N = Convert.ToDouble(tbATS_N.Text.Replace(",", "."));
            double dblATS_E = Convert.ToDouble(tbATS_E.Text.Replace(",", "."));
            double dblATS_H = Convert.ToDouble(tbATS_H.Text.Replace(",", "."));

            double dblATSprevious_E = dblATS_E;
            double dblATSprevious_N = dblATS_N;

            // read the ATS orientation
            double dblATS_orientation = Convert.ToDouble(tbOrientationCorr.Text.Replace(",", "."));
            atsCoords.Add(new ATSCoords() { Name = strATSname, N = dblATS_N, E = dblATS_E, H = dblATS_H, OrientationGons = dblATS_orientation });

            // Read the prisms
            int iPrismCounter = -1;
            foreach (var item in dgPrismCoords.Items.OfType<PrismCoords>())
            {
                var name = item.Name;
                var E = Convert.ToDouble(item.E);
                var N = Convert.ToDouble(item.N);
                var H = Convert.ToDouble(item.H);
                prismCoords.Add(new PrismCoords() { Name = name, E = E, N = N, H = H });
                iPrismCounter++;
            }

           
            // Read the Orientation observations that are ticked
            // count the number of orientation observations

            int iOrrObs = -1;
            foreach (var item in dgOrientation.Items.OfType<OrientationObservations>())
            {
                var used = item.IsUsed;

                if (used == true)
                {
                    var name = item.Name;
                    var Ha = Convert.ToDouble(item.HA);
                    var Va = Convert.ToDouble(item.VA);
                    var Sd = Convert.ToDouble(item.SD); // prism constant added
                    orientationObs.Add(new OrientationObservations() { Name = name, HA = Ha, VA = Va, SD = Sd });
                    iOrrObs++;
                }
            }

            if (iOrrObs < 1)
            {
                MessageBox.Show("Insufficient targets for Transformation");
                goto ExitPoint;
            }

            double dblDs = 100;

            List<TransCoords> transCoords = new(); // Resets the list
            do
            {
                // compute the coordinates from the gka observations + orientation correction
                transCoords = new(); // Resets the list

                for (int i = 0; i <= iOrrObs; i++)
                {
                    string strName = orientationObs[i].Name;

                    //MessageBox.Show(strName);

                    double Hdist = Math.Sin(dblGonToRad * orientationObs[i].VA) * orientationObs[i].SD;  // Prism constant added
                    double dblBearing = (orientationObs[i].HA + dblATS_orientation) * dblGonToRad;
                    double dblVAngle = 100.0 - orientationObs[i].VA;
                    double Eobs = Math.Round((dblATS_E + (Math.Sin(dblBearing) * Hdist)), 3);
                    double Nobs = Math.Round((dblATS_N + (Math.Cos(dblBearing) * Hdist)), 3);
                    double Hobs = Math.Round((Math.Tan(dblVAngle) * Hdist), 3);

                    //MessageBox.Show(strName + ": " + Eobs + "  " + Nobs + "  " + Hobs);

                    // now find the coordinates of this target from the prism coordinate list
                    double Ectrl = 0.0;
                    double Nctrl = 0.0;
                    double Hctrl = 0.0;
                    for (int j = 0; j <= iPrismCounter; j++)
                    {
                        if (prismCoords[j].Name == strName)
                        {
                            Ectrl = prismCoords[j].E;
                            Nctrl = prismCoords[j].N;
                            Hctrl = prismCoords[j].H;
                            goto Continue1;
                        }
                    }
Continue1:
                   // MessageBox.Show("Name " + strName + " Emain: " + Ectrl + " Nmain: " + Nctrl + " Hmain: " + Hctrl + " Elocal: " + Eobs + " Nlocal: " + Nobs + " Hlocal: " + Hobs);
                    transCoords.Add(new TransCoords() { Name = strName, Elocal = Eobs, Nlocal = Nobs, Hlocal = Hobs, Emain = Ectrl, Nmain = Nctrl, Hmain = Hctrl, ObservedHA = orientationObs[i].HA, ObservedVA = orientationObs[i].VA, ObservedSD = orientationObs[i].SD });
                }

                // Now compute the transformation
                // SEE HIRVONEN P 219  NOMENCLATURE IS AS PER 16.21
                // XP = (A+(C*PointX)+(D*PointY));
                // YP = (B + (C * PointY) - (D * PointX));

                double PU = 0.0, PL = 0.0, NU = 0.0, NL = 0.0, QU = 0.0, QL = 0.0;
                double R = 0.0, S = 0.0, M = 0.0, VV = 0.0;
                double A, B, C, D, SK, T;
                

                for (int i = 0; i <= iOrrObs; i++)
                {
                    NL = NL + 1;
                    PL = PL + transCoords[i].Nlocal;
                    QL = QL + transCoords[i].Elocal;
                    PU = PU + transCoords[i].Nmain;
                    QU = QU + transCoords[i].Emain;
                    NU = NU + ((transCoords[i].Nlocal * transCoords[i].Nlocal) + (transCoords[i].Elocal * transCoords[i].Elocal));
                    R = R + ((transCoords[i].Nlocal * transCoords[i].Nmain) + (transCoords[i].Elocal * transCoords[i].Emain));
                    S = S + ((transCoords[i].Elocal * transCoords[i].Nmain) - (transCoords[i].Nlocal * transCoords[i].Emain));

                }

                M = (NL * NU) - (PL * PL) - (QL * QL);
                A = ((NU * PU) - (PL * R) - (QL * S)) / M;
                B = ((NU * QU) - (QL * R) + (PL * S)) / M;
                C = ((NL * R) - (PL * PU) - (QL * QU)) / M;
                D = ((PL * QU) - (QL * PU) + (NL * S)) / M;

                SK = Math.Sqrt((C * C) + (D * D));
                T = Math.Atan(D / C);

                // Compute the transformed value for the ATS

                NP = Math.Round((A + (C * dblATS_N) + (D * dblATS_E)), 3);
                EP = Math.Round((B + (C * dblATS_E) - (D * dblATS_N)), 3);
                dblDs = Math.Round(Math.Sqrt(Math.Pow((dblATSprevious_E - EP), 2) + Math.Pow((dblATSprevious_N - NP), 2)), 3);

                // Update the ATS coordinates
                dblATSprevious_E = EP;
                dblATSprevious_N = NP;

                iIterationCounter++;
                dblATS_E = EP;
                dblATS_N = NP;
                //MessageBox.Show("ATS: " + EP + " " + NP + " Iteration: " + iIterationCounter);

                // Compute the orientation correction
                dblATS_orientation = 0;

                for (int i = 0; i <= iOrrObs; i++)
                {
                    var answer = Join(dblATS_E, dblATS_N, transCoords[i].Emain, transCoords[i].Nmain);
                    double dblBearingAB = answer.Item1 / dblGonToRad;    // bearing in Gons

                    //double dblDiff = Math.Round(dblBearingAB - transCoords[i].ObservedHA, 4);
                    //MessageBox.Show(Convert.ToString(dblDiff));

                    dblATS_orientation = dblATS_orientation+(dblBearingAB - transCoords[i].ObservedHA);
                }

                dblATS_orientation = Math.Round(dblATS_orientation / (iOrrObs + 1),6);

                tbOrientationCorr.Text = String.Format("{0:0.0000}", dblATS_orientation);


            } while ((dblDs > 0.001));

            // Now compute the Elevation of the total station
            double dblMeanElevation = 0.0;
            for (int i = 0; i <= iOrrObs; i++)
            {
                var answer = Join(dblATS_E, dblATS_N, transCoords[i].Emain, transCoords[i].Nmain);
                double dblDistAB = answer.Item2;
                double dblDh = Math.Cos(dblGonToRad * transCoords[i].ObservedVA) * transCoords[i].ObservedSD;
                dblMeanElevation = dblMeanElevation + transCoords[i].Hmain - dblDh;
            }

            dblATS_H = Math.Round((dblMeanElevation / (iOrrObs + 1)), 3);

            //MessageBox.Show("ATS H: " + dblATS_H);

            // Compute the differences


            dblMeanElevation = 0.0;
            for (int i = 0; i <= iOrrObs; i++)
            {
                var answer = Join(dblATS_E, dblATS_N, transCoords[i].Emain, transCoords[i].Nmain);
                double dblBearingAB = answer.Item1 / dblGonToRad;
                double dblDistAB = answer.Item2;
                transCoords[i].dS = Math.Round(dblDistAB- (Math.Sin(dblGonToRad * transCoords[i].ObservedVA) * transCoords[i].ObservedSD),3);
                transCoords[i].OrrCorr = Math.Round(dblATS_orientation-(dblBearingAB - transCoords[i].ObservedHA),4);
                double dblDh = Math.Cos(dblGonToRad * transCoords[i].ObservedVA) * transCoords[i].ObservedSD;
                double dblATSZ = transCoords[i].Hmain - dblDh;
                transCoords[i].dH = Math.Round((dblATS_H - dblATSZ),3);
            }

            // Update the display
            dgResults.ItemsSource = transCoords;

            tbATS_E.Text = String.Format("{0:0.000}", dblATS_E);  
            tbATS_N.Text = String.Format("{0:0.000}", dblATS_N);
            tbATS_H.Text = String.Format("{0:0.000}", dblATS_H);

ExitPoint:;

        }




        public void btnPrismCoords_Click(object sender, RoutedEventArgs e)
        {
            List<PrismCoords> prismCoords = new();

            var ofd = new Microsoft.Win32.OpenFileDialog() { Filter = "CSV Files (*.csv)|*.csv" };
            var result = ofd.ShowDialog();
            if (result == false)
            {
                return;
            }

            string strSelectedFile = ofd.FileName;

            // Parse the CSV file to the PrismCoords list

            var reader = new StreamReader(File.OpenRead(strSelectedFile));
            
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                string[] strList = line.Split(',');
                double dblE = Convert.ToDouble(strList[1]);
                double dblN = Convert.ToDouble(strList[2]);
                double dblH = Convert.ToDouble(strList[3]);
                prismCoords.Add(new PrismCoords() { Name = strList[0], E = dblE, N = dblN, H = dblH });
            }

            int iPrismsCounter = prismCoords.Count;

            // Check for duplicate prisms

            int i = 0;
            do
            {
                string strName = prismCoords[i].Name;

                int iCount = 0;
                int j = 0;
                do
                {
                    if (prismCoords[j].Name== strName) { iCount++; }
                    j++;
                } while (j < iPrismsCounter);


                if (iCount>1)
                {
                    MessageBox.Show("Duplicate: "+prismCoords[i].Name);
                    prismCoords[i].Name = prismCoords[i].Name + "****";
                }
                i++;
            } while (i < iPrismsCounter);


            // Write this to the DataGrid
            //this.dgPrismCoords.ItemsSource = prismCoords;
            dgPrismCoords.ItemsSource = prismCoords;
        }

        private void btnOrientation_Click(object sender, RoutedEventArgs e)
        {
            double dblPrismConstant = -0.017;

            List<OrientationObservations> orientationObs = new();

            double dblHa;
            double dblVa;
            double dblSd;

            var ofd = new Microsoft.Win32.OpenFileDialog() { Filter = "gka Files (*.gka)|*.gka" };
            var result = ofd.ShowDialog();
            if (result == false)
            {
                return;
            }

            // Read the prisms
            List<PrismCoords> prismCoords = new();
            int iNoOfPrisms = -1;
            foreach (var item in dgPrismCoords.Items.OfType<PrismCoords>())
            {
                var name = item.Name;
                var E = Convert.ToDouble(item.E);
                var N = Convert.ToDouble(item.N);
                var H = Convert.ToDouble(item.H);
                prismCoords.Add(new PrismCoords() { Name = name, E = E, N = N, H = H });
                iNoOfPrisms++;
            }




            string strSelectedFile = ofd.FileName;

            // Parse the gka observation file to the orientationObs list

            var reader = new StreamReader(File.OpenRead(strSelectedFile));
            String[] strObservations = new String[50];
            int iObsCounter = 0;
            int iRoundsCounter = 0;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();

                if (line != null)
                {
                    if (line.Trim() == "#END11") { iRoundsCounter++; }
                    if ((line.Trim() == "#GNV11") || (line.Trim() == "#END11")) goto Next;

                    string[] strList = line.Split(',');
                    Int64 iNumber = Convert.ToInt64(strList[0]);
                    if (iNumber < 1000)
                    {
                        strObservations[iObsCounter] = line;
                        iObsCounter++;
                    }
                }
Next:
                continue;
            }

            // Extract all the observations, change all obs to Face 1 observations
            for (int i = 0; i < iObsCounter; i++)
            {
                string[] strList = strObservations[i].Split(',');
                string strName = strList[1];
                dblHa = Convert.ToDouble(strList[10]);
                dblVa = Convert.ToDouble(strList[12]);
                dblSd = Convert.ToDouble(strList[7]);

                if (dblSd==0)
                {
                    MessageBox.Show("No reading: " + strName+" face "+ strList[6]);
                }

                if (strList[6] == "2")
                {
                    if (dblHa >= 200)
                    {
                        dblHa = dblHa - 200.0;
                    }
                    else
                    {
                        dblHa = dblHa + 200.0;
                    }
                    dblVa = 400 - dblVa;
                }

                orientationObs.Add(new OrientationObservations() { IsUsed = true, Name = strList[1], HA = dblHa, VA = dblVa, SD = dblSd });
            }

            // sort orientationObs by Name

            orientationObs.Sort(delegate (OrientationObservations x, OrientationObservations y)
        {
            return x.Name.CompareTo(y.Name);
        });


            int iPrismCounter = iObsCounter / (iRoundsCounter * 2);

            // Compute the mean values

            List<OrientationObservations> meanObs = new();

            //MessageBox.Show("iPrismCounter " + iPrismCounter + "  iObsCounter " + iObsCounter + "  iRoundsCounter " + iRoundsCounter);

            int iSet = iRoundsCounter * 2;
            for (int i = 0; i < iObsCounter; i += (iRoundsCounter * 2))
            {
                //MessageBox.Show(orientationObs[i].Name);
                string strName = orientationObs[i].Name;
                dblHa = 0;
                dblVa = 0;
                dblSd = 0;
                for (int j = i; j < i + (iRoundsCounter * 2); j++)
                {

                    //MessageBox.Show("** " + orientationObs[j].Name);
                    dblHa = dblHa + orientationObs[j].HA;
                    dblVa = dblVa + orientationObs[j].VA;
                    dblSd = dblSd + orientationObs[j].SD;
                }

                dblHa = Math.Round(dblHa / iSet, 6);
                dblVa = Math.Round(dblVa / iSet, 6);
                dblSd = Math.Round(dblSd / iSet, 3) + dblPrismConstant;
                bool IsUsedFlag = false;

                // Check whether this point exists in the prism list
                for (int k = 0; k <= iNoOfPrisms; k++)
                {
                    if (strName == prismCoords[k].Name)
                    {
                        IsUsedFlag = true;
                        goto NextStep;
                    }
                }

NextStep:
                meanObs.Add(new OrientationObservations() { IsUsed = IsUsedFlag, Name = strName, HA = dblHa, VA = dblVa, SD = dblSd });
            }

            // bind this to the DataGrid
            this.dgOrientation.ItemsSource = meanObs;
        }

        private void dgOrientation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void dgPrismCoords_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void tbATS_name_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void tbATS_E_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void tbATS_H_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void tbOrientationCorr_TextChanged(object sender, TextChangedEventArgs e)
        {

        }



        private void btnTSfile_Click(object sender, RoutedEventArgs e)
        {
            double dblGonToRad = 0.0157079633;

            var strLine = new List<string>();

            // read the ATS coordinate
            string strATSname = tbATS_name.Text;
            double dblATS_N = Convert.ToDouble(tbATS_N.Text.Replace(",", "."));
            double dblATS_E = Convert.ToDouble(tbATS_E.Text.Replace(",", "."));
            double dblATS_H = Convert.ToDouble(tbATS_H.Text.Replace(",", "."));

            double dblOrrCorr = Convert.ToDouble(tbOrientationCorr.Text.Replace(",", "."));

            List<PrismCoords> prismCoords = new();
            int iPrismCounter = -1;
            foreach (var item in dgPrismCoords.Items.OfType<PrismCoords>())
            {
                var name = item.Name;
                var E = Convert.ToDouble(item.E);
                var N = Convert.ToDouble(item.N);
                var H = Convert.ToDouble(item.H);
                prismCoords.Add(new PrismCoords() { Name = name, E = E, N = N, H = H });
                iPrismCounter++;
            }

            // Now generate the ts file data
            // String[] strLine = new String[500];

            // First Line
            strLine.Add("ST2," + strATSname + ",S9 HP,0,0,0,0,0," + Convert.ToString(dblATS_E) + "," + Convert.ToString(dblATS_N) + "," + Convert.ToString(dblATS_H));
            int iLineIndex = 1;
            for (int i = 0; i < iPrismCounter; i++)
            {
                string strName = prismCoords[i].Name;

                string strE = String.Format("{0:0.000}", prismCoords[i].E);
                string strN = String.Format("{0:0.000}", prismCoords[i].N);
                string strH = String.Format("{0:0.000}", prismCoords[i].H);

                var answer = Join(dblATS_E, dblATS_N, prismCoords[i].E, prismCoords[i].N);
                double HA_F1 = (answer.Item1 / dblGonToRad) - dblOrrCorr;

                if (HA_F1 < 0) { HA_F1 = HA_F1 + 400; }

                double HA_F2 = HA_F1 - 200;
                if (HA_F2 < 0) { HA_F2 = HA_F2 + 400.0; }
                HA_F1 = Math.Round(HA_F1, 4);
                HA_F2 = Math.Round(HA_F2, 4);
                double SD = Math.Round(Math.Sqrt(Math.Pow((dblATS_E - prismCoords[i].E), 2) + Math.Pow((dblATS_N - prismCoords[i].N), 2) + Math.Pow((dblATS_H - prismCoords[i].H), 2)), 3);
                double dblDh = prismCoords[i].H - dblATS_H;

                double VA_F1 = Math.Round(100 - (Math.Asin(dblDh / SD) / dblGonToRad), 4);
                double VA_F2 = Math.Round((400 - VA_F1), 4);
                string strLineIndex = Convert.ToString(iLineIndex);
                string strHA_F1 = String.Format("{0:0.0000}", HA_F1);
                string strHA_F2 = String.Format("{0:0.0000}", HA_F2); 
                string strVA_F1 = String.Format("{0:0.0000}", VA_F1); 
                string strVA_F2 = String.Format("{0:0.0000}", VA_F2);
                string strSD = String.Format("{0:0.0000}", SD);
                string strMode = (SD > 30) ? "3" : "2";
                string strTargetCoords = Convert.ToString(prismCoords[i].E) + "," + Convert.ToString(prismCoords[i].N) + "," + Convert.ToString(prismCoords[i].H);

                
                string strPart1 = strLineIndex + "," + strName + "," + strHA_F1 + ",0," + strVA_F1 + ",0," + strSD + "," + strMode + ",1,0," + strTargetCoords + ",0,0,1";
                string strPart2 = "," + strHA_F1 + "," + strVA_F1 + "," + strSD + ",0,1,0";

                string strFace1 = strPart1;
                strLine.Add(strFace1);

                string strPart3 = strLineIndex + "," + strName + "," + strHA_F2 + ",0," + strVA_F2 + ",0," + strSD + "," + strMode + ",2,0," + strTargetCoords + ",0,0,1";
                string strPart4 = "," + strHA_F2 + "," + strVA_F2 + "," + strSD + ",0,1,0";
                string strFace2 = strPart3;
                strLine.Add(strFace2);

                iLineIndex++;

            }

            strLine.Add("TheEnd");

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "ts files (*.ts)|*.ts";

            if (saveFileDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveFileDialog.FileName, Convert.ToString(strLine));

                string strFileName = saveFileDialog.FileName.Replace(".ts", "");
                strFileName = strFileName + ".ts";
                if (File.Exists(strFileName)) { File.Delete(strFileName); }
                
                using (StreamWriter writetext = new(strFileName, false))
                {
                    int j = 0;
                    do
                    {
                        writetext.WriteLine(strLine[j]);
                        j++;
                    } while (strLine[j] != "TheEnd");
                    writetext.Close();
                }


            }




        }





        //======[Methods]======================================


        Tuple<double, double> Join(double YA, double XA, double YB, double XB)
        {
            //Purpose:
            //  To compute bearing, horizontal distance between 2 points
            //Input:
            //  Cartesian coordinates of points A,B
            //Output:
            //  bearingAB, disanceAB;
            //Use:
            //  var answer = gnaSurvey.Join(dblYA, dblXA,dblYB, dblXB);
            //  double dblBearingAB = answer.Item1;
            //  double dblDistAB = answer.Item2;

            // Math.Pow((Math.POW((YB-YA),2) + Math.POW((XB-XA),2)),0.5)


            double SAB = Math.Pow((Math.Pow((YB - YA), 2) + Math.Pow((XB - XA), 2)), 0.5);
            double DirAB = 0.0;
            if (SAB != 0.0)
            {

                DirAB = Math.Acos((XB - XA) / SAB);
                if ((YB - YA) < 0.0)
                {
                    DirAB = (2.0 * Math.PI) - DirAB;
                }
            }

            SAB = Math.Round(SAB, 4);
            DirAB = Math.Round(DirAB, 8);

            return new Tuple<double, double>(DirAB, SAB);
        }


        public List<PrismCoords> readPrismCoordinates()
        {
            int iPrismCounter = -1;
            List<PrismCoords> prismCoords = new();
            foreach (var item in dgPrismCoords.Items.OfType<PrismCoords>())
            {
                var name = item.Name;
                var E = Convert.ToDouble(item.E);
                var N = Convert.ToDouble(item.N);
                var H = Convert.ToDouble(item.H);
                prismCoords.Add(new PrismCoords() { Name = name, E = E, N = N, H = H });
                iPrismCounter++;
            }
            return prismCoords;
        }

    }





    //======[Classes]======================================
    public class TransCoords
    {
        public string? Name { get; set; }
        public double Nlocal { get; set; }
        public double Elocal { get; set; }
        public double Hlocal { get; set; }
        public double Nmain { get; set; }
        public double Emain { get; set; }
        public double Hmain { get; set; }
        public double dS { get; set; }
        public double dH { get; set; }
        public double OrrCorr { get; set; }

        public double ObservedHA { get; set; }

        public double ObservedVA { get; set; }

        public double ObservedSD { get; set; }
    }


    public class ATSCoords
    {
        private string? nameValue;
        public string Name
        {
            get { return nameValue; }
            set { nameValue = value; }
        }
        public double N { get; set; }
        public double E { get; set; }
        public double H { get; set; }
        public double OrientationGons { get; set; }

        //public static ObservableCollection<PrismCoords>
    }

    public class PrismCoords
    {
        private string? nameValue;
        public string Name
        {
            get { return nameValue; }
            set { nameValue = value; }
        }
        public double N { get; set; }
        public double E { get; set; }
        public double H { get; set; }

        //public static ObservableCollection<PrismCoords>
    }

    public class OrientationObservations
    {
        public bool IsUsed { get; set; }
        public string? Name { get; set; }
        public double HA { get; set; }
        public double VA { get; set; }
        public double SD { get; set; }
    }



}


