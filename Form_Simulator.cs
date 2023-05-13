using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GachaSimulator
{
    public partial class Form_Simulator : Form
    {
        public Form_Simulator()
        {
            InitializeComponent();
        }

        private void Form_Simulator_Load(object sender, EventArgs e)
        {
            InitialProbs();
        }

        private Dictionary<int, double> Probs { get; set; }
        private bool previousBadResult { get; set; } = false;

        private Dictionary<int, int> DrawLimit(int max, CancellationToken token)
        {
            var drawTimes = Convert.ToInt32(tb_DrawTimes.Text);

            Dictionary<int, int> drawResultCount = new Dictionary<int, int>()
            {
                { 2, 0 },
                { 3, 0 },
                { 4, 0 },
                { 5, 0 },
                { 6, 0 },  //歪
                { 7, 0 }   //限定
            };

            Random random = new Random();
            int count = 0;

            for (int i = 0; i < drawTimes; i++)
            {
                token.ThrowIfCancellationRequested();

                var drawResult = DrawOnce(count, max, random);

                if (drawResult == 6)
                {
                    drawResult = LimitedOrNot(random) ? 7 : 6;
                    previousBadResult = drawResult == 6;
                    count = 0;  //抽到六星重置计数器
                }
                else
                {
                    count++;  //未抽到六星累加
                }

                lock (drawResultCount)
                {
                    drawResultCount[drawResult]++;
                }
            }

            return drawResultCount;
        }

        public Dictionary<int, int> DrawNormal(int max, CancellationToken token)
        {
            var drawTimes = Convert.ToInt32(tb_DrawTimes.Text);

            Dictionary<int, int> drawResultCount = new Dictionary<int, int>()
            {
                { 2, 0 },
                { 3, 0 },
                { 4, 0 },
                { 5, 0 },
                { 6, 0 }
            };

            Random random = new Random();
            int count = 0;

            for (int i = 0; i < drawTimes; i++)
            {
                token.ThrowIfCancellationRequested();

                var drawResult = DrawOnce(count, max, random);

                if (drawResult == 6)
                {
                    count = 0;  //抽到六星重置计数器
                }
                else
                {
                    count++;  //未抽到六星累加
                }

                lock (drawResultCount)
                {
                    drawResultCount[drawResult]++;
                }
            }

            return drawResultCount;
        }

        private int DrawOnce(int count, int max, Random random)
        {
            try
            {
                int drawResult = 0;

                if (count == max)
                {
                    drawResult = 6;
                }
                else
                {
                    CalculateProbs(count);

                    double randomNumber = random.NextDouble() * 100;
                    double cumulativeProbability = 0;

                    foreach (var item in Probs)
                    {
                        cumulativeProbability += item.Value;
                        if (randomNumber <= cumulativeProbability)
                        {
                            drawResult = item.Key;
                            break;
                        }
                    }
                }

                // 无论结果如何，都重新计算概率
                CalculateProbs(count);

                return drawResult;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //提升6星概率加权分摊至每个区间进行扣除
        private void CalculateProbs(int count)
        {
            try
            {
                InitialProbs(); // 初始化每档概率

                if (count > 60)
                {
                    var increment = (count - 60) * 2.5; // 计算6级的概率增量

                    // 创建 Probs 集合的副本并修改它
                    Dictionary<int, double> newProbs = new Dictionary<int, double>(Probs);
                    newProbs[6] += increment;

                    var deduction = increment / 2;
                    newProbs[3] -= deduction;
                    newProbs[4] -= deduction;

                    // 替换原始的 Probs 集合
                    Probs = newProbs;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void InitialProbs()
        {
            Probs = new Dictionary<int, double>
            {
                { 6, 1.5 },
                { 5, 8.5 },
                { 4, 40 },
                { 3, 45 },
                { 2, 5 }
            };
        }

        private bool LimitedOrNot(Random random)
        {
            if (!previousBadResult)
            {
                double randomNumber = random.NextDouble() * 100;
                if (randomNumber < 50)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        private bool isDrawing { get; set; } = false;

        private CancellationTokenSource cts = new CancellationTokenSource();

        private async void btn_Start_Click(object sender, EventArgs e)
        {
            switch (isDrawing)
            {
                case false:
                    {
                        this.isDrawing = true;
                        tsl_Status.Text = "抽卡中";
                        btn_Start.Text = "停止抽卡";
                        tb_DrawTimes.Enabled = false;
                        rbtn_Limit.Enabled = false;
                        rbtn_Normal.Enabled = false;

                        cts = new CancellationTokenSource();

                        try
                        {
                            var ret = new Dictionary<int, int>();
                            tsl_Status.Text = "开始第一轮抽取";
                            if (rbtn_Limit.Checked)
                            {
                                ret = await Task.Run(() => DrawLimit(80, cts.Token));
                            }
                            else
                            {
                                ret = await Task.Run(() => DrawNormal(80, cts.Token));
                            }
                            ResultAnalyze(dgv_80, ret);

                            ret.Clear();
                            tsl_Status.Text = "开始第二轮抽取";
                            if (rbtn_Limit.Checked)
                            {
                                ret = await Task.Run(() => DrawLimit(70, cts.Token));
                            }
                            else
                            {
                                ret = await Task.Run(() => DrawNormal(70, cts.Token));
                            }
                            ResultAnalyze(dgv_70, ret);

                            tsl_Status.Text = "抽取完毕";
                        }
                        catch (OperationCanceledException)
                        {
                            tsl_Status.Text = "抽卡中止";
                        }
                        finally
                        {
                            this.isDrawing = false;
                            btn_Start.Text = "开始抽卡";

                            tb_DrawTimes.Enabled = true;
                            rbtn_Limit.Enabled = true;
                            rbtn_Normal.Enabled = true;

                            cts = null;
                        }
                    }
                    break;

                case true:
                    {
                        this.isDrawing = false;
                        btn_Start.Text = "开始抽卡";

                        tb_DrawTimes.Enabled = true;
                        rbtn_Limit.Enabled = true;
                        rbtn_Normal.Enabled = true;

                        if (cts != null)
                        {
                            cts.Cancel();
                            cts = null;
                        }
                    }
                    break;
            }
        }

        private void tb_DrawTimes_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void ResultAnalyze(DataGridView dgv, Dictionary<int, int> result)
        {
            dgv.Rows.Clear();
            dgv.Columns.Clear();

            dgv.Columns.Add("Column1", "星级");
            dgv.Columns.Add("Column1", "次数");
            dgv.Columns.Add("Column2", "概率");
            dgv.Columns.Add("Column3", "期望");

            int totalDraws = result.Values.Sum();  // 总抽卡次数

            switch (rbtn_Limit.Checked)
            {
                case true:
                    {
                        double sixStarProbability = (double)(result[6] + result[7]) / totalDraws * 100;  // 六星概率
                        double sixStarExpectation = 1 / sixStarProbability * 100;  // 六星期望
                        double limitedProbability = (double)result[7] / totalDraws * 100;  // 限定卡概率
                        double limitedExpectation = 1 / limitedProbability * 100;  // 限定卡期望

                        // 剩下各级别的概率
                        Dictionary<int, double> otherProbabilities = new Dictionary<int, double>
                        {
                            { 2, (double)result[2] / totalDraws * 100 },
                            { 3, (double)result[3] / totalDraws * 100 },
                            { 4, (double)result[4] / totalDraws * 100 },
                            { 5, (double)result[5] / totalDraws * 100 },
                            { 7, (double)result[7] / totalDraws * 100 }
                        };

                        dgv.Rows.Add("二星", result[2], otherProbabilities[2] + "%", "-");
                        dgv.Rows.Add("三星", result[3], otherProbabilities[3] + "%", "-");
                        dgv.Rows.Add("四星", result[4], otherProbabilities[4] + "%", "-");
                        dgv.Rows.Add("五星", result[5], otherProbabilities[5] + "%", "-");
                        dgv.Rows.Add("六星", result[6] + result[7], sixStarProbability + "%", sixStarExpectation);
                        dgv.Rows.Add("限定", result[7], limitedProbability + "%", limitedExpectation);
                    }
                    break;

                case false:
                    {
                        double sixStarProbability = (double)result[6] / totalDraws * 100;  // 六星概率
                        double sixStarExpectation = 1 / sixStarProbability * 100;  // 六星期望

                        // 剩下各级别的概率
                        Dictionary<int, double> otherProbabilities = new Dictionary<int, double>
                        {
                            { 2, (double)result[2] / totalDraws * 100 },
                            { 3, (double)result[3] / totalDraws * 100 },
                            { 4, (double)result[4] / totalDraws * 100 },
                            { 5, (double)result[5] / totalDraws * 100 }
                        };

                        dgv.Rows.Add("二星", result[2], otherProbabilities[2] + "%", "-");
                        dgv.Rows.Add("三星", result[3], otherProbabilities[3] + "%", "-");
                        dgv.Rows.Add("四星", result[4], otherProbabilities[4] + "%", "-");
                        dgv.Rows.Add("五星", result[5], otherProbabilities[5] + "%", "-");
                        dgv.Rows.Add("六星", result[6], sixStarProbability + "%", sixStarExpectation);
                    }
                    break;
            }
        }
    }
}