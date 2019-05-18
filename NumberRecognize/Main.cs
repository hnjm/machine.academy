﻿using Macademy;
using Macademy.OpenCL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NumberRecognize
{
    public partial class Main : Form
    {
        private Calculator calculator = null;
        private Network network = null;
        Bitmap bitmap;
        Bitmap bitmapDownscaled;
        private Network.TrainingPromise trainingPromise = null;
        Timer trainingtimer = new Timer();
        TrainingWindow progressDialog = null;
        NetworkConfig layerConfWindow = new NetworkConfig();
        DateTime trainingStart;

        private int targetWidth = 28, targetHeight = 28;
        private int downScaleWidth = 20, downScaleHeight = 20;

        public Main()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comboRegularization.SelectedIndex = 2;
            comboCostFunction.SelectedIndex = 1;

            calculator = new Calculator(null);

            bitmap = new Bitmap(targetWidth, targetHeight, System.Drawing.Imaging.PixelFormat.Format16bppRgb565);
            bitmapDownscaled = new Bitmap(downScaleWidth, downScaleHeight, System.Drawing.Imaging.PixelFormat.Format16bppRgb565);
            ClearBitmap();
            pictureBox1.Image = bitmap;

            comboBox1.Items.Add("Use CPU calculation");
            comboBox1.SelectedIndex = 0;
            foreach (var device in ComputeDevice.GetDevices())
            {
                string item = "[" + device.GetPlatformID() + ":" + device.GetDeviceID() + ", " + device.GetDeviceType().ToString() + "] " + device.GetName().Trim() + " " + (device.GetGlobalMemorySize() / (1024*1024) ) + "MB";
                comboBox1.Items.Add(item);
            }

            trainingtimer.Interval = 300;
            trainingtimer.Tick += Trainingtimer_Tick;

            InitRandomNetwork();

        }

        private void Trainingtimer_Tick(object sender, EventArgs e)
        {
            if (progressDialog != null && trainingPromise != null)
            {
                var timespan = (DateTime.Now - trainingStart);
                string time = new TimeSpan(timespan.Hours, timespan.Minutes, timespan.Seconds).ToString();

                progressDialog.UpdateResult(trainingPromise.GetTotalProgress(), trainingPromise.IsReady(), "Training... Epochs done: " + trainingPromise.GetEpochsDone(), time);
                if (trainingPromise.IsReady())
                {
                    trainingPromise = null;
                    progressDialog = null;
                    trainingtimer.Stop();
                }
            }
        }

        private void InitRandomNetwork()
        {
            List<int> layerConfig = layerConfWindow.GetLayerConfig();

            network = Network.CreateNetworkInitRandom(layerConfig.ToArray(), new SigmoidActivation(), new DefaultWeightInitializer());
            lblnetcfg.Text = String.Join("x", network.GetLayerConfig());
            network.AttachName("MNIST learning DNN");
            network.AttachDescription("MNIST learning DNN using " + layerConfig.Count + " layers in structure: (" + string.Join(", ", layerConfig) + " ). Creation date: " + DateTime.Now.ToString() );
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
                calculator = new Calculator();
            else
                calculator = new Calculator(ComputeDevice.GetDevices()[comboBox1.SelectedIndex - 1]);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (layerConfWindow.ShowDialog() != DialogResult.OK)
                return;

            InitRandomNetwork();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (network != null)
            {
                saveFileDialog1.Filter = "JSON File|*.json";
                saveFileDialog1.Title = "Save training data";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(saveFileDialog1.FileName, network.ExportToJSON());
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "JSON File|*.json";
            openFileDialog1.Title = "Save training data";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string file = System.IO.File.ReadAllText(openFileDialog1.FileName);
                    var newNetwork = Network.CreateNetworkFromJSON(file);
                    network = newNetwork;

                    lblnetcfg.Text = String.Join("x", network.GetLayerConfig());
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Error when loading network: " + exc.ToString(), "Error",MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadTestDataFromFiles(List<TrainingSuite.TrainingData> trainingData, String labelFileName, String imgFileName, Action<int> progressHandler = null)
        {
            var labelData = System.IO.File.ReadAllBytes(labelFileName);
            int labelDataOffset = 8; //first 2x32 bits are not interesting for us.

            var imageData = System.IO.File.ReadAllBytes(imgFileName);
            int imageDataOffset = 16; //first 4x32 bits are not interesting for us.
            int imageSize = targetWidth * targetHeight;

            for (int i = labelDataOffset; i < labelData.Length; i++)
            {
                int trainingSampleId = i - labelDataOffset;
                int label = labelData[i];
                float[] input = new float[imageSize];
                float[] output = new float[10];
                for (int j = 0; j < targetHeight; j++)
                {
                    for (int k = 0; k < targetWidth; k++)
                    {
                        int offsetInImage = j * targetWidth + k;
                        byte pixelColor = imageData[imageDataOffset + trainingSampleId * imageSize + offsetInImage];
                        input[offsetInImage] = ((float)pixelColor) / 255.0f;
                        //bitmap.SetPixel(k, j, Color.FromArgb(255, 255- pixelColor, 255 - pixelColor, 255 - pixelColor));
                    }
                }

                if ( progressHandler != null)
                {
                    if (i % 200 == 0)
                    {
                        progressHandler(((i - labelDataOffset)*100) / (labelData.Length-labelDataOffset)); 
                    }
                }

                /*
                pictureBox1.Refresh();
                System.Threading.Thread.Sleep(100);*/
                output[label] = 1.0f;
                trainingData.Add(new TrainingSuite.TrainingData(input, output));
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string imgFile = "";
            string labelFile = "";

            openFileDialog1.Filter = "Image Training data (Image)|*.*";
            openFileDialog1.Title = "Open Training images file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                imgFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            openFileDialog1.Filter = "Training data (Label)|*.*";
            openFileDialog1.Title = "Open Training labels file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                labelFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            LoadingWindow wnd = new LoadingWindow();
            wnd.Text = "Loading training data";

            List<TrainingSuite.TrainingData> trainingData = new List<TrainingSuite.TrainingData>();

            System.Threading.Thread thread = new System.Threading.Thread(()=> {
                LoadTestDataFromFiles(trainingData, labelFile, imgFile, (x)=> { wnd.SetProgress(x); });
                wnd.Finish();
            });

            thread.Start();

            if (wnd.ShowDialog() != DialogResult.OK)
                return;

            var trainingSuite = new TrainingSuite(trainingData);
            trainingSuite.config.miniBatchSize = (int)numMiniBatchSize.Value;
            trainingSuite.config.learningRate = (float)numLearningRate.Value;
            trainingSuite.config.regularizationLambda = (float)numLambda.Value;
            trainingSuite.config.shuffleTrainingData= checkShuffle.Checked;

            if (comboRegularization.SelectedIndex == 0)
                trainingSuite.config.regularization = TrainingSuite.TrainingConfig.Regularization.None;
            else if (comboRegularization.SelectedIndex == 0)
                trainingSuite.config.regularization = TrainingSuite.TrainingConfig.Regularization.L1;
            else if (comboRegularization.SelectedIndex == 0)
                trainingSuite.config.regularization = TrainingSuite.TrainingConfig.Regularization.L2;

            if (comboCostFunction.SelectedIndex == 0)
                trainingSuite.config.costFunction = new MeanSquaredErrorFunction();
            else if (comboCostFunction.SelectedIndex == 1)
                trainingSuite.config.costFunction = new CrossEntropyErrorFunction();

            trainingSuite.config.epochs = (int)numEpoch.Value;

            trainingStart = DateTime.Now;
            trainingPromise = network.Train(trainingSuite, calculator);
            trainingtimer.Start();


            progressDialog = new TrainingWindow(trainingPromise);
            progressDialog.ShowDialog();
        }

        private void ClearBitmap()
        {
            for (int i = 0; i < targetHeight; i++)
            {
                for (int j = 0; j < targetWidth; j++)
                {
                    bitmap.SetPixel(j, i, Color.White);
                }
            }

            for (int i = 0; i < downScaleHeight; i++)
            {
                for (int j = 0; j < downScaleWidth; j++)
                {
                    bitmapDownscaled.SetPixel(j, i, Color.White);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ClearBitmap();
            pictureBox1.Refresh();
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            paintPixel(e);
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
        }

        private void paintPixel(MouseEventArgs e)
        {
            var centerX = (int)Math.Floor(((float)e.X / (float)pictureBox1.Width) * (float)(bitmapDownscaled.Size.Width - 1));
            var centerY = (int)Math.Floor(((float)e.Y / (float)pictureBox1.Height) * (float)(bitmapDownscaled.Size.Height- 1));

            Action<int, int, int> applyColor = (x,y,c) => {
                int xClamped = Math.Max(0, Math.Min(x, bitmapDownscaled.Size.Width - 1));
                int yClamped = Math.Max(0, Math.Min(y, bitmapDownscaled.Size.Height - 1));
                bitmapDownscaled.SetPixel(xClamped, yClamped, Color.FromArgb(255,c,c,c) );
            };

            applyColor(centerX, centerY, 0);

            //upscale
            for (int i = 0; i < targetHeight; i++)
            {
                for (int j = 0; j < targetWidth; j++)
                {
                    float xRatio = (float)j / (float)(targetWidth - 1);
                    float yRatio = (float)i / (float)(targetHeight - 1);

                    float ds_x = xRatio * (float)downScaleWidth;
                    float ds_y = yRatio * (float)downScaleHeight;

                    float xBias = ds_x - (float)Math.Floor(ds_x);
                    float yBias = ds_y - (float)Math.Floor(ds_y);

                    int ds_x_int = (int)ds_x;
                    int ds_y_int = (int)ds_y;

                    bool isAtXBorder = ds_x_int >= downScaleWidth - 1;
                    bool isAtYBorder = ds_y_int >= downScaleHeight - 1;

                    float v = 1;
                    float vx = 1;
                    float vy = 1;
                    float vxy = 1;

                    if (!isAtXBorder && !isAtYBorder)
                    {
                        v = bitmapDownscaled.GetPixel(ds_x_int, ds_y_int).GetBrightness();
                        vx = bitmapDownscaled.GetPixel(ds_x_int + 1, ds_y_int).GetBrightness();
                        vy = bitmapDownscaled.GetPixel(ds_x_int, ds_y_int + 1).GetBrightness();
                        vxy = bitmapDownscaled.GetPixel(ds_x_int + 1, ds_y_int + 1).GetBrightness();
                    }

                    float b1 = vx * xBias + (1.0f - xBias) * v;
                    float b2 = vxy * xBias + (1.0f - xBias) * vy;
                    float b3 = b2 * yBias + (1.0f - yBias) * b1;

                    int c = (int)(b3 * 255.0f);
                    bitmap.SetPixel(j, i, Color.FromArgb(255, c, c, c));
                }
            }

            pictureBox1.Refresh();
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                paintPixel(e);
            }
        }

        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        {
        }

        private int ClassifyOutput(float[] output)
        {
            float largest = -1;
            int resultIdx = -1;
            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] > largest)
                {
                    largest = output[i];
                    resultIdx = i;
                }
            }
            return resultIdx;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            float[] input = new float[targetWidth * targetHeight];

            for (int i = 0; i < targetHeight; i++)
            {
                for (int j = 0; j < targetWidth; j++)
                {
                    var color = bitmap.GetPixel(j,i).GetBrightness();
                    input[i * targetWidth + j] = 1.0f - color; //in input 1.0f is black, 0.0f is white
                }
            }

            var output = network.Compute(input, calculator);

            int resultIdx = ClassifyOutput(output);

            lblResult.Text = "Results:\nI think you drew a " + resultIdx + "\nOutput was:\n";
            lblResult.Text += string.Join("\n ", output);
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button7_Click(object sender, EventArgs e)
        {
            string imgFile = "";
            string labelFile = "";

            openFileDialog1.Filter = "Test data (Image)|*.*";
            openFileDialog1.Title = "Open Test images file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                imgFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            openFileDialog1.Filter = "Test data (Label)|*.*";
            openFileDialog1.Title = "Open Test labels file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                labelFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            List<TrainingSuite.TrainingData> trainingData = new List<TrainingSuite.TrainingData>();

            LoadingWindow wnd = new LoadingWindow();
            wnd.Text = "Testing network";
            int success = 0;

            var thread = new System.Threading.Thread(() => {

                wnd.SetText("Opening training file...");
                LoadTestDataFromFiles(trainingData, labelFile, imgFile, (x)=> { wnd.SetProgress(x/10); });

                wnd.SetProgress(10);

                wnd.SetText("Testing...");
                for (int i = 0; i < trainingData.Count; i++)
                {
                    var output = network.Compute(trainingData[i].input, calculator);

                    int resultIdx = ClassifyOutput(output);
                    int expectedIdx = ClassifyOutput(trainingData[i].desiredOutput);
                    if (resultIdx == expectedIdx)
                        ++success;

                    if (i % 200 == 0)
                        wnd.SetProgress(10 + ((i*90) / trainingData.Count));
                }

                wnd.Finish();
            });

            thread.Start();

            if (wnd.ShowDialog() != DialogResult.OK)
                return;

            float perc = ((float)success / (float)trainingData.Count) * 100.0f;

            MessageBox.Show("Test completed with " + trainingData.Count + " examples. Successful were: " + success + " (" + perc + "%)", "Test complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            string imgFile = "";

            openFileDialog1.Filter = "Image Training data (Image)|*.*";
            openFileDialog1.Title = "Open Training images file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                imgFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            byte[] content = System.IO.File.ReadAllBytes(imgFile);

            int imgid = (int)numericUpDown2.Value;
            int imageDataOffset = 16; //first 4x32 bits are not interesting for us.
            int imageSize = targetWidth * targetHeight;

            for (int i = 0; i < targetHeight; i++)
            {
                for (int j = 0; j < targetWidth; j++)
                {
                    int c = 255 - content[imageDataOffset + imageSize * imgid + i * targetWidth + j];
                    bitmap.SetPixel(j, i, Color.FromArgb(255, c, c, c));
                }
            }
            pictureBox1.Refresh();
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
        }
    }
}
