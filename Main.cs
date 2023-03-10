using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Microsoft.Win32;
using TeremunsCarrierAssistant.Events;
using TeremunsCarrierAssistant.FleetCarrier;

namespace TeremunsCarrierAssistant {
    public partial class Main : Form {
        private readonly FlightPlan plan = new FlightPlan();
        private bool flightPlanLoaded = false;
        
        private readonly RegistryKey _saveManager;
        private JournalHandler journalHandler;
        private readonly string currentUserPath;
        
        private int currentIndex = 0;
        
        private Jump jump;
        private Refuel refuel;
        
        private static DateTime targetTime;

        private bool onJourney, isJumping, isRefueled = false;
        private bool refuelManually = true;


        public Main() {
            InitializeComponent();
            currentUserPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            // Register KeyInputs
            Keyboard vInput = new Keyboard();
            jump = new Jump(vInput, 7000);
            refuel = new Refuel(vInput);
            
            UpdateJournal();
            textCurrentLocation.Text = @"Current Location: " + journalHandler.locationData.StarSystem;
        }
        
        // Teremun Methods
        private async void Assistant() {
            await Task.Run(() => {
                onJourney = true;

                while (onJourney) {
                    UpdateJournal(); // Get Latest Journal
                
                    textNextJump.Text = "... " + plan.SystemName[currentIndex];

                    //Check if the player is Jumping
                    if (isJumping) continue;
                    
                    /*
                    if (!isRefueled) {
                        if (plan.FuelUsed[currentIndex] > 0)
                            if (!refuelManually) refuel.Perform(plan.FuelUsed[currentIndex], Convert.ToInt32(tritiumPosition.Value));
                            else isRefueled = true;
                        else isRefueled = true;

                    }*/
                        
                    PlayControlSound("error_start.wav");
                    
                    textCurrentLocation.Text = "Current Location: " + journalHandler.locationData.StarSystem;
                    textDebug.Text = "Jumping: This carrier will now try to jump to " + plan.SystemName[currentIndex] + "...";
                    jump.Perform(plan.SystemName[currentIndex]);

                    UpdateJournal();
                    targetTime = journalHandler.carrierJumpRequestData.DepartureTime;
                    textDebug.Text = "Jump: " + targetTime.ToLongDateString() + " - Now: " + DateTime.Now.ToUniversalTime(); 
                        
                    if (checkIfJumpBugged()) { //REPLOT
                        textDebug.Text = "Jump taking to long replotting...";
                        countdown.Stop();
                        jump.Perform(plan.SystemName[currentIndex]);
                        textDebug.Text = "Jump taking to long replotting, application will freeze till jump cooldown is completed...";
                        Thread.Sleep(50000); // Jump Cooldown
                        jump.Perform(plan.SystemName[currentIndex]);
                            
                        targetTime = journalHandler.carrierJumpRequestData.DepartureTime;
                        
                        targetTime = targetTime.AddMinutes(4).AddSeconds(50); // Add the cooldown
                        textDebug.Text = "Jumping to " + journalHandler.carrierJumpRequestData.SystemName + "...";
                        
                        countdown.Start();

                            
                        UpdateJournal();
                        targetTime = journalHandler.carrierJumpRequestData.DepartureTime;
                    }

                    targetTime = targetTime.AddMinutes(4).AddSeconds(50); // Add the cooldown
                    textDebug.Text = "Jumping to " + journalHandler.carrierJumpRequestData.SystemName + "...";
                        
                    countdown.Start();
                        
                    isJumping = true;
                }
            });
            
        }

        private void PlayControlSound(string filename) {
            SoundPlayer player = new SoundPlayer();
            player.SoundLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"./../../" + filename);
            player.PlaySync();
        }
        
        private void Timer_Elapsed(object sender, ElapsedEventArgs e) {
            textDebug.Text = "Jump: " + targetTime.ToLongTimeString() + " - Now: " + DateTime.Now.ToUniversalTime(); 

            if (DateTime.Now.ToUniversalTime() < targetTime) return;
            
            countdown.Stop();
            currentIndex++;

            listJumps.Items.Clear();
            for (int i = currentIndex; i < plan.SystemName.Count; i++) {
                listJumps.Items.Add(plan.SystemName[i]);
            }

            if (currentIndex < plan.SystemName.Count) {
                textNextJump.Text = plan.SystemName[currentIndex];
            } else { 
                // Jumps Completed
                textNextJump.Text = "... ???";
                btnStart.Visible = true;
                onJourney = false;
            }
            
            isRefueled = false;
            isJumping = false;
        }

        private bool isPlayerInSystem() => journalHandler.locationData.StarSystem.Equals(plan.SystemName[currentIndex]);
        private bool checkIfJumpBugged() => targetTime > DateTime.UtcNow.AddMinutes(25).AddSeconds(30);

        private void UpdateJournal() {
            DirectoryInfo dirInfo = new DirectoryInfo($"{currentUserPath}\\Saved Games\\Frontier Developments\\Elite Dangerous\\");
            FileInfo file = (from f in dirInfo.GetFiles("*.log") orderby f.LastWriteTime descending select f).First();

            string original = $"{currentUserPath}\\Saved Games\\Frontier Developments\\Elite Dangerous\\" + file;
            string copy = $"{currentUserPath}\\Saved Games\\Frontier Developments\\Elite Dangerous\\logCopy.teremun";
            
            try {  
                File.Copy(original, copy, true);  
            } catch (IOException) { 
                // Ignore
            }

            journalHandler = new JournalHandler(copy);

            if (!onJourney)
            {
                PlayControlSound("dial_chevron_beep2.wav");
            }
        }

        private void UpdateCarrierOperations() {
            jump = new Jump(jump.keyboard, (int)galaxyMapLag.Value);
            refuel = new Refuel(refuel.keyboard);
        }
        
        // Form Event Handlers
        private void btnOpenFileDialog_Click(object sender, EventArgs e) {
            selectCarrierRoute.ShowDialog(this);
            plan.ConvertFlightPlan(selectCarrierRoute);

            foreach (string system in plan.SystemName.ToArray()) listJumps.Items.Add(system);

            textDebug.Text = "Waiting for activating the assistant...";
            textNextJump.Text = "... " + plan.SystemName[currentIndex];
            flightPlanLoaded = true;
        }
        private void btnStart_Click(object sender, EventArgs e) {
            SoundPlayer player = new SoundPlayer();
            player.SoundLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"./../../error_start.wav");
            player.PlaySync();
            
            if (flightPlanLoaded) {
                btnStart.Visible = false;
                Clipboard.SetText(plan.SystemName[currentIndex]);
                Assistant();
            }
        }
        private void btnUpdateLocation_Click(object sender, EventArgs e) {
            UpdateJournal();

            textCurrentLocation.Text = "Current Location: " + journalHandler.locationData.StarSystem;

            if (flightPlanLoaded) {
                if (isPlayerInSystem()) currentIndex++;
                textNextJump.Text = "... " + plan.SystemName[currentIndex];
                
                listJumps.Items.Clear();
                for (int i = currentIndex; i < plan.SystemName.Count; i++) listJumps.Items.Add(plan.SystemName[i]);
            }
            
        }

        private void galaxyBuffer_ValueChanged(object sender, EventArgs e) => UpdateCarrierOperations();
        private void tritiumItemSlot_ValueChanged(object sender, EventArgs e) => UpdateCarrierOperations();
        private void checkRefuelMan_CheckedChanged(object sender, EventArgs e) {
            refuelManually = checkRefuelMan.Checked;
        }
    }
}
