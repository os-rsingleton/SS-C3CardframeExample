using System;
using System.Collections.Generic;
using Crestron.SimplSharp;                              // For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.ThreeSeriesCards;          // For 3-series card, cardframes

namespace CardframeExample
{
    public class ControlSystem : CrestronControlSystem
    {
        /// <summary>
        /// Properties...
        /// </summary>
        private CenCi33 myCenCi33;
        private C3com3 myC3com3;
        private List<ComPort> myC3coms;

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem()
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;


                //Is the cardframe object already instantiated?
                if (myCenCi33 == null)
                {
                    //The cardframe object is not instantiated.
                    //Instantiate the cardframe object for use with our processor using IP-ID 08.
                    myCenCi33 = new CenCi33(0x08, this);

                    //Define an event handler for changes in the ether online state of the cardframe.
                    myCenCi33.OnlineStatusChange += new OnlineStatusChangeEventHandler(myCenCi33_OnlineStatusChange);

                    //Register the cardframe.
                    if (myCenCi33.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    {
                        //Registration of the cardframe failed.
                        //Output the reason to the console and set the cardframe object to null.
                        CrestronConsole.PrintLine(String.Format("Failed to register Cen-Ci3-3 -- {0}", myCenCi33.RegistrationFailureReason));
                        myCenCi33 = null;
                    }
                    else
                    {
                        //Registration of the cardframe was successful.
                        //Check to see if the installed C3COM-3 card object is instantiated.
                        if (myC3com3 == null)
                        {
                            //The com card object is not instantiated.
                            //Instantiate the com card object as a member of the cardframe in slot 3.
                            myC3com3 = new C3com3(0x03, myCenCi33);

                            //Define an event handler for changes in the online state of the com card.
                            myC3com3.OnlineStatusChange += new OnlineStatusChangeEventHandler(myC3com3_OnlineStatusChange);

                            //Check to see if the com ports collection is instantiated.
                            if (myC3coms == null)
                            {
                                //The com ports collection is not instantiated.
                                //Instantiate the com ports collection.
                                myC3coms = new List<ComPort>();

                                //Loop through the com card's available com ports,
                                //set their specs and register them.
                                foreach (ComPort com in myC3com3.ComPorts)
                                {
                                    //Set the specs --> Baud, Data bits, Parity, Stop bits, Protocol type, Hardware handshaking, and Software handshaking.
                                    com.SetComPortSpec(
                                        ComPort.eComBaudRates.ComspecBaudRate9600,
                                        ComPort.eComDataBits.ComspecDataBits8,
                                        ComPort.eComParityType.ComspecParityNone,
                                        ComPort.eComStopBits.ComspecStopBits1,
                                        ComPort.eComProtocolType.ComspecProtocolRS232,
                                        ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                        ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone, false
                                    );

                                    //Register the port
                                    if (com.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                                    {
                                        //Registration of the com port failed.
                                        //Output the reason to the console.
                                        CrestronConsole.PrintLine(String.Format("Failed to register ComPort {0} -- {1}", com.ID, com.DeviceRegistrationFailureReason));
                                    }
                                    else
                                    {
                                        //Registration of the com port succeeded.
                                        //Output the ID to the console.
                                        CrestronConsole.PrintLine(String.Format("Successfully registered ComPort {0}", com.ID));
                                    }

                                    //Add the com port object to the com ports collection.
                                    myC3coms.Add(com);
                                }
                            }

                            //Register the com card object.
                            if (myC3com3.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                            {
                                //Registration of the com card object failed.
                                //Output the reason to the console and set the object to null.
                                CrestronConsole.PrintLine(String.Format("Failed to register C3com3 -- {0}", myC3com3.RegistrationFailureReason));
                                myC3com3 = null;
                            }
                        }
                    }
                }


                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(ControlSystem_ControllerEthernetEventHandler);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            try
            {
                CrestronConsole.AddNewConsoleCommand(SendToCardframeComport, "SendToCardframeComport", "Send a string to a comport on a connected cardframe.", ConsoleAccessLevelEnum.AccessOperator);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }

        /// <summary>
        /// Event Handler for online status changes between the cardframe and the program
        /// </summary>
        /// <param name="currentDevice"></param>
        /// <param name="args"></param>
        void myCenCi33_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            if (args.DeviceOnLine == true)
            {
                //The device is online.
            }
        }

        /// <summary>
        /// Event Handler for online status changes between the card and the program.
        /// </summary>
        /// <param name="currentDevice"></param>
        /// <param name="args"></param>
        void myC3com3_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            if (args.DeviceOnLine == true)
            {
                //The device is online.
            }
        }

        /// <summary>
        /// Send a string to a com port on a card in a cardframe.
        /// </summary>
        /// <param name="args"></param>
        public void SendToCardframeComport(string args)
        {
            //Setup required variables
            int frameIPID = 0;              //When multiple cardframes are registered with the system, 
                                            //this var can be used to identify a specified cardframe 
                                            //within a collection.

            int framePort = 0;              //This var is used to identify the port on a C3Com-3 card.
            string serialOut = "";          //This var is used to pass a string out the specified comport.

            //Use Try/Catch to handle possible exceptions.
            try
            {
                //Check to see if an argument was passed.
                if (args.Length > 1)
                {
                    //Split up the parts of the passed arguments and put them in an array.
                    string[] parms = args.Split(' ');

                    //Loop through the array deciding what to do as needed.
                    foreach (string parm in parms)
                    {
                        //Is the argument a "?" or "--help"?
                        if (parm.Contains("?") || parm.ToUpper().Contains("--HELP"))
                        {
                            //The argument is a question mark or the "--help" flag.
                            //Output the proper command format to the console and stop.
                            CrestronConsole.PrintLine("SendToCardframeComport -i:[IP-ID] -p:[Comport #]");
                            return;
                        }

                        //Is the argument the "-i" or "-I"
                        if (parm.ToUpper().Contains("-I"))
                        {
                            //The argument is an "i" which indicates a cardframe's IP-ID
                            //This is an example of how you would get the IP-ID to use
                            //in identifying a single cardframe from a collection of 
                            //registered cardframes.
                            //
                            //IN THIS PROGRAM IT IS AN REFEREMCE/EXAMPLE ONLY AND DOES NOT 
                            //ACTUALLY PROVIDE ANY FUNCTIONALITY.
                            string[] ipidParts = parm.Split(':');
                            frameIPID = Convert.ToInt32(ipidParts[1]);
                        }
                        //Is the argument a "-p" or "-P"?
                        else if (parm.ToUpper().Contains("-P"))
                        {
                            //The argument is a "p" which indicates the desired comport on
                            //C3COM-3 Card.
                            string[] comportParts = parm.Split(':');
                            framePort = Convert.ToInt32(comportParts[1]);
                        }
                        else
                        {
                            //Put whats left of the arguments in a string to send out of
                            //the specified serial port.
                            serialOut += String.Format("{0} ", parm);
                        }
                    }

                    //Send the string out of the specified comport.
                    myC3coms[framePort - 1].Send(serialOut.Trim());
                }
            }
            catch (Exception e)
            {
                //Catch the exception if one occurs.
                //Output the exception to the console.
                CrestronConsole.PrintLine("Exception occured in sending data to the comport {0} --> {1}", framePort, e.Message);
            }
            finally { }
        }
    }
}