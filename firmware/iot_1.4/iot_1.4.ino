// ========== MAYBE LIST ==========
// Switch polling to listener or smarter polling

#include <WiFi.h>
#include <ESP32Servo.h>
#include <SimpleDHT.h>
#include <FirebaseESP32.h>
#include <time.h>
#include <LiquidCrystal.h>

#define fan1Pin 13
#define fan2Pin 12
#define light1Pin 14
#define light2Pin 27
#define LDRpin 33
#define DHTpin 25
#define scannerIntPin 4

const char* ssid = "Xom Tro";
const char* password = "xomtro247";

//const char* ssid = "84 NTT (2.4G)";
//const char* password = "84857937";

#define FIREBASE_HOST "test-119a8-default-rtdb.asia-southeast1.firebasedatabase.app"
#define FIREBASE_AUTH "YK6wmkGJDolANaT4B3ylSi34pW49qgKoYYP8cKUb"

const char* ntpServer = "pool.ntp.org";
const long  gmtOffset_sec = 7 * 3600; // GMT+7
const int   daylightOffset_sec = 0;

SimpleDHT11 dht11(25);

LiquidCrystal lcd(5, 18, 19, 21, 22, 23);

Servo door;
bool doorState, fan1State, fan2State, light1State, light2State = false;
bool enrollFlag, enrollDone, delFingerprintData, enrollError, delFingerError = false; // States are edited only twice by firmware in 1 class session, the rest is managed by GUI
bool inSession, autoMode = true;
bool lcdRunning = false;

float temp, hum;
int student_number, photo;

volatile bool poll_flag = false;
hw_timer_t *timer0 = NULL;
void IRAM_ATTR pollTimer() {
  poll_flag = true;
  timerStop(timer0);
}

volatile bool dht_flag = false;
hw_timer_t *timer1 = NULL;
void IRAM_ATTR dhtTimer() {
  dht_flag = true;
  timerStop(timer1);
}

hw_timer_t *timer2 = NULL;
void IRAM_ATTR lcdTimer() {
  timerStop(timer2);
  lcdRunning = false;
  lcd.clear();
}

uint16_t fingerIDEnroll = 1;

FirebaseData firebaseData;
FirebaseAuth auth;
FirebaseConfig config;

float temperature, humidity;
bool dhtError;

void setup() {
  Serial.begin(115200);
  Serial2.begin(57600, SERIAL_8N1, 16, 17);

  lcd.begin(16, 2);
  lcd.clear();

  door.setPeriodHertz(50);
  door.attach(26, 500, 2500);
  door.write(180);

  pinMode(fan1Pin, OUTPUT);
  pinMode(fan2Pin, OUTPUT);
  pinMode(light1Pin, OUTPUT);
  pinMode(light2Pin, OUTPUT);
  pinMode(LDRpin, INPUT);
  pinMode(scannerIntPin, INPUT);
  
  WiFi.begin(ssid, password);
  Serial.printf("Connecting to WiFi %s ...\n", ssid);
  lcd.print("Connecting to");
  lcd.setCursor(0, 1);
  lcd.print("WiFi");

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
    lcd.print(".");
  }
  Serial.println("\nWiFi connected");
  lcd.clear();
  lcd.print("WiFi connected");

  configTime(gmtOffset_sec, daylightOffset_sec, ntpServer); 
  Serial.print("Syncing time");
  lcd.clear();
  lcd.print("Syncing time");
  int timeout = 0;
  while (timeout < 20) { // 10 seconds
    delay(500);
        struct tm timeinfo;
    if (getLocalTime(&timeinfo)) {
      Serial.println("\nTime synchronized");
      lcd.clear();
      lcd.print("Time synchronized");
      break;
    }
    Serial.print(".");
    Serial.print(".");
    lcd.print(".");
    timeout++;
  }
  
  if (timeout >= 20) {
    Serial.println("\nSync timeout");
    lcd.clear();
    lcd.print("Sync timeout");
  }

  config.host = FIREBASE_HOST;
  config.signer.tokens.legacy_token = FIREBASE_AUTH;
  
  Firebase.begin(&config, &auth);
  Firebase.reconnectWiFi(true);
  
  Serial.println("Firebase initialized");
  lcd.clear();
  lcd.print("Firebase");
  lcd.setCursor(0, 1);
  lcd.print("initialized");
  delay(3000);
  if (Firebase.getInt(firebaseData, "/current/studentNumber"))
  student_number = firebaseData.intData();
  Serial.print("student_number: ");
  Serial.println(student_number);

  Firebase.getBool(firebaseData, "/track/autoMode");
  autoMode = firebaseData.boolData();
  Serial.print("autoMode: ");
  Serial.println(autoMode);

  Serial.println("Initializing timers");
  lcd.clear();
  lcd.print("Initializing");
  lcd.setCursor(0, 1);
  lcd.print("timers...");

  // Poll timer
  timer0 = timerBegin(1000000);
  timerAttachInterrupt(timer0, &pollTimer);
  timerAlarm(timer0, 1000000, true, 0); // 1 second, auto-reload
  timerStop(timer0);
  timerWrite(timer0, 0);
  timerStart(timer0);

  // DHT timer
  timer1 = timerBegin(1000000);
  timerAttachInterrupt(timer1, &dhtTimer);
  timerAlarm(timer1, 10000000, true, 0); // 10 second, auto-reload, in reality is 11s (maybe because of rounding)
  timerStop(timer1);
  timerWrite(timer1, 0);
  timerStart(timer1);

  // LCD timer
  timer2 = timerBegin(1000000);
  timerAttachInterrupt(timer2, &lcdTimer);
  timerAlarm(timer2, 3000000, false, 0); // 3 second, auto-reload
  timerStop(timer2);
  timerWrite(timer2, 0);

  delay(2000);
}

String getCurrentTime() {
  struct tm timeinfo;
  if(!getLocalTime(&timeinfo)){
    Serial.println("Failed to obtain time");
    return "Time Error";
  }
  
  char timeString[64];
  strftime(timeString, sizeof(timeString), "%H:%M:%S %d-%m-%Y", &timeinfo);
  return String(timeString);
}

void read_temp_hum(void) {
  byte temperatureByte = 0;
  byte humidityByte = 0;
  
  int err = SimpleDHTErrSuccess;

  if ((err = dht11.read(&temperatureByte, &humidityByte, NULL)) != SimpleDHTErrSuccess) { // Error
    Serial.print("DHT11 read failed, err=");
    Serial.println(err);
    return;
  }
  
  temp = (float)temperatureByte;
  hum = (float)humidityByte;

  photo = analogRead(LDRpin);
  photo = map(photo, 0, 4095, 100, 0);

  String currentTime = getCurrentTime();

  Serial.printf("Temperature: %.1f C; Humidity: %.1f %%; Light: %d %%; Current time: %s\n\n", temp, hum, photo, currentTime.c_str());
  
  Firebase.setFloat(firebaseData, "/current/temperature", temp);
  Firebase.setFloat(firebaseData, "/current/humidity", hum);
  Firebase.setFloat(firebaseData, "/current/light", photo);
  Firebase.setString(firebaseData, "/current/lastUpdate", currentTime);
}

void doorLock(bool flag) {
  if (flag == false) {
    door.write(100);
  } else {
    door.write(0);
  }
  Serial.printf("Door state: %s\n\n", flag ? "OPENED" : "CLOSED");
}

void timer2Run(uint64_t t){ // Max 3s
  timerStop(timer2);
  timerWrite(timer2, 3000000 - t);
  timerStart(timer2);
  lcdRunning = true;
}

// ============================= Fingerprint identify ==============================

void AutoIdentify() {
  String results, name;
  int seat;

  Serial.println("Identifying fingerprint...");

  // Send the AutoIdentify command
  Serial2.write((uint8_t)0xEF);
  Serial2.write((uint8_t)0x01); // Header

  for (int i = 0; i < 4; i++) {
    Serial2.write((uint8_t)0xFF);
  } // Module address

  Serial2.write((uint8_t)0x01); // Package identifier

  Serial2.write((uint8_t)0x00);
  Serial2.write((uint8_t)0x08); // Package length

  Serial2.write((uint8_t)0x32); // Instruction code

  Serial2.write((uint8_t)0x03); // Security level

  Serial2.write((uint8_t)0xFF);
  Serial2.write((uint8_t)0xFF); // ID number (0xFFFF = 1:N search)

  Serial2.write((uint8_t)0x00);
  Serial2.write((uint8_t)0x00); // Parameters

  Serial2.write((uint8_t)0x02); // Checksum high byte
  Serial2.write((uint8_t)0x3C); // Checksum low byte

  // Ưait for and parse the response
  results = parseAutoIdentifyResponse();
  if (results != "error"){
    String currentTime = getCurrentTime();
    
    // Check if Firebase operation succeeded
    if (Firebase.getBool(firebaseData, "/students/" + results + "/attendance_flag")) {
      if (firebaseData.dataType() == "boolean") {
        bool currentAttendance = firebaseData.boolData();
        
        if (currentAttendance == false){ // Entering
          if (student_number == 0){ // First to arrive, loads turn on, door unlocks
            inSession = true;
            student_number++;
            doorState = true;
            fan1State = true;
            fan2State = true;
            light1State = true;
            light2State = true;
            
            Firebase.setBool(firebaseData, "/ioState/doorState", doorState);
            Firebase.setBool(firebaseData, "/ioState/fan1State", fan1State);
            Firebase.setBool(firebaseData, "/ioState/fan2State", fan2State);
            Firebase.setBool(firebaseData, "/ioState/light1State", light1State);
            Firebase.setBool(firebaseData, "/ioState/light2State", light2State);

            Firebase.setBool(firebaseData, "/students/" + results + "/attendance_flag", true);
            Firebase.setInt(firebaseData, "/current/studentNumber", student_number);
            Firebase.setString(firebaseData, "/students/" + results + "/time_of_attendance", currentTime);

          } else { // Not first to enter
            student_number++;
            Firebase.setBool(firebaseData, "/students/" + results + "/attendance_flag", true);
            Firebase.setInt(firebaseData, "/current/studentNumber", student_number);
            Firebase.setString(firebaseData, "/students/" + results + "/time_of_attendance", currentTime);
          }

          if (Firebase.getString(firebaseData, "/students/" + results + "/id")) {
            name = firebaseData.stringData();
            Serial.printf("ID: %s\n", name.c_str());
          } else {
            name = "Unknown";
            Serial.println("Failed to get name");
          }

          if (Firebase.getInt(firebaseData, "/students/" + results + "/seat")) {
            seat = firebaseData.intData();
            Serial.printf("Ghế %d\n", seat);
          } else {
            seat = 0;
            Serial.println("Failed to get seat");
          }
          
          lcd.clear();
          lcd.print("MSSV: " + name);
          lcd.setCursor(0, 1);
          lcd.print("Seat: " + String(seat));
          timer2Run(3000000);

        } else { // Exiting
          if (student_number == 1){ // Last to leave, loads turn off
            inSession = false;
            student_number--;
            doorState = false;
            fan1State = false;
            fan2State = false;
            light1State = false;
            light2State = false;
            
            Firebase.setBool(firebaseData, "/ioState/doorState", doorState);
            Firebase.setBool(firebaseData, "/ioState/fan1State", fan1State);
            Firebase.setBool(firebaseData, "/ioState/fan2State", fan2State);
            Firebase.setBool(firebaseData, "/ioState/light1State", light1State);
            Firebase.setBool(firebaseData, "/ioState/light2State", light2State);

            Firebase.setBool(firebaseData, "/students/" + results + "/attendance_flag", false);
            Firebase.setInt(firebaseData, "/current/studentNumber", student_number);
            Firebase.setString(firebaseData, "/students/" + results + "/time_of_attendance", "n/a");

          } else { // Not last to leave
            student_number--;
            Firebase.setBool(firebaseData, "/students/" + results + "/attendance_flag", false);
            Firebase.setInt(firebaseData, "/current/studentNumber", student_number);
            Firebase.setString(firebaseData, "/students/" + results + "/time_of_attendance", "n/a");
          }

          if (Firebase.getString(firebaseData, "/students/" + results + "/id")) {
            name = firebaseData.stringData();
            Serial.printf("ID: %s\n", name.c_str());
          } else {
            name = "Unknown";
            Serial.println("Failed to get name");
          }

          if (Firebase.getInt(firebaseData, "/students/" + results + "/seat")) {
            seat = firebaseData.intData();
            Serial.printf("Ghế %d\n", seat);
          } else {
            seat = 0;
            Serial.println("Failed to get seat");
          }
          
          lcd.clear();
          lcd.print("MSSV: " + name);
          lcd.setCursor(0, 1);
          lcd.print("Seat: " + String(seat));
          timer2Run(3000000);
        }
      } else {
        Serial.println("Error: attendance_flag is not boolean");
      }
    } else {
      Serial.println("Firebase getBool failed for attendance_flag");
      Serial.println(firebaseData.errorReason());
    }
  }
}

String parseAutoIdentifyResponse() {
  const unsigned long timeout = 10000; // 10 second timeout
  unsigned long startTime = millis();
  bool gotSearchResult = false;
  
  while (millis() - startTime < timeout && !gotSearchResult) {
    if (Serial2.available() >= 17) {
      // Look for packet header
      if (Serial2.read() == 0xEF && Serial2.read() == 0x01) {
        // Module address
        for (int i = 0; i < 4; i++) {
          Serial2.read();
        }
        
        // Check package identifier
        byte packageID = Serial2.read();
        if (packageID == 0x07) {
          // Read package length
          byte lengthHigh = Serial2.read();
          byte lengthLow = Serial2.read();
          uint16_t packageLength = (lengthHigh << 8) | lengthLow;
          
          // Read confirmation code
          byte confirmationCode = Serial2.read();
          
          // Read parameters
          byte parameters = Serial2.read();
          
          // Read ID number
          byte idHigh = Serial2.read();
          byte idLow = Serial2.read();
          uint16_t fingerID = (idHigh << 8) | idLow;
          
          // Read score
          byte scoreHigh = Serial2.read();
          byte scoreLow = Serial2.read();
          uint16_t matchScore = (scoreHigh << 8) | scoreLow;
          
          // Skip checksum
          Serial2.read();
          Serial2.read();
          
          // Parse based on parameters field
          switch (parameters) {
            case 0x00: // Instruction validity test
              if (confirmationCode != 0x00) {
                Serial.println("Instruction validity test failed");
                lcd.clear();
                lcd.print("Ins validity");
                lcd.setCursor(0, 1);
                lcd.print("Test failed");
                timer2Run(2000000);
                return "error";
              }
              break;
              
            case 0x01: // Get image result
              if (confirmationCode != 0x00) {
                Serial.println("Get image failed");
                lcd.clear();
                lcd.print("Get image");
                lcd.setCursor(0, 1);
                lcd.print("Failed");
                timer2Run(2000000);
                return "error";
              }
              break;
              
            case 0x05: // Search results
              gotSearchResult = true;
              if (confirmationCode == 0x00) {
                Serial.println("FINGERPRINT MATCHED!");
                Serial.print("User ID: ");
                Serial.println(fingerID);

                String dbKey = "fingerprint_data";
                if (fingerID < 10) {
                  dbKey += "0"; // Add leading 0
                }
                dbKey += String(fingerID);
                return dbKey;
              } else {
                Serial.print("Unknown fingerprint.");
                lcd.clear();
                lcd.print("Unknown");
                lcd.setCursor(0, 1);
                lcd.print("Fingerprint");
                timer2Run(2000000);
                return "error";
              }
              break;
              
            default:
              Serial.println("Unexpected parameter, please try again");
              lcd.clear();
              lcd.print("Error");
              lcd.setCursor(0, 1);
              lcd.print("Please try again");
              timer2Run(2000000);
              return "error";
              break;
          }
        }
      }
    }
    delay(10);
  }
  
  if (!gotSearchResult) {
    Serial.println("Timeout, please try again");
    lcd.clear();
    lcd.print("Timeout");
    lcd.setCursor(0, 1);
    lcd.print("Please try again");
    timer2Run(2000000);
    return "error";
  }
  return "error";
}

// ============================= Fingerprint identify ==============================


// ============================= Fingerprint enroll ==============================

void autoEnroll(byte numEntries = 2, bool requireFingerRemoval = true, bool allowOverwrite = false, bool checkDuplicate = true) {
  enrollError = false;

  Serial.println("Finger ID: ");
  Serial.println(fingerIDEnroll);
  Serial.print("Number of captures required: ");
  Serial.println(numEntries);
    
  byte parameterBits = 0x00;
  if (!requireFingerRemoval) parameterBits |= (1 << 5);
  if (allowOverwrite) parameterBits |= (1 << 3);
  if (!checkDuplicate) parameterBits |= (1 << 4);
    
  // Send enrollment command
  Serial2.write((uint8_t)0xEF);
  Serial2.write((uint8_t)0x01); // Header

  for (int i = 0; i < 4; i++) {
    Serial2.write((uint8_t)0xFF);
  } // Module address

  Serial2.write((uint8_t)0x01); // Package identifier

  // Length
  uint16_t length = 0x0008;
  Serial2.write((uint8_t)(length >> 8));
  Serial2.write((uint8_t)(length & 0xFF));
    
  Serial2.write((uint8_t)0x31); // Instruction code

  // ID Number
  Serial2.write((uint8_t)(fingerIDEnroll >> 8));
  Serial2.write((uint8_t)(fingerIDEnroll & 0xFF));

  Serial2.write((uint8_t)numEntries); // Number of entries

  // Parameters
  Serial2.write((uint8_t)0x00);
  Serial2.write((uint8_t)parameterBits);

  // Calculate checksum: PackageID + Length + Instruction + ID + Entries + Parameters
  uint16_t checksum = 0x01;
  checksum += (length >> 8) & 0xFF;
  checksum += length & 0xFF;
  checksum += 0x31;
  checksum += (fingerIDEnroll >> 8) & 0xFF;
  checksum += fingerIDEnroll & 0xFF;
  checksum += numEntries;
  checksum += 0x00;
  checksum += parameterBits;

  // Send checksum (high byte first)
  Serial2.write((uint8_t)(checksum >> 8));
  Serial2.write((uint8_t)(checksum & 0xFF));

  Serial.print("Enrollment command sent for ID: ");
  Serial.println(fingerIDEnroll);
   
   // Start monitoring the enrollment process
  monitorEnrollment();
}

void monitorEnrollment() {
  bool enrollmentComplete = false;
  byte currentStep = 0;
  byte currentEntry = 0;
  
  while (!enrollmentComplete && !enrollError) {
    if (Serial2.available()) {
      // Read and process response packet
      if (readEnrollmentResponse(&currentStep, &currentEntry)) {
        // Check if enrollment is complete
        if (currentStep == 0x06) { // Template storage step
          enrollmentComplete = true;
        }
      }
    }
  }
}

bool readEnrollmentResponse(byte* step, byte* entry) {
  if (Serial2.available() < 12) return false; // Minimum packet size
  
  // Look for header
  if (Serial2.read() != 0xEF) return false;
  if (Serial2.read() != 0x01) return false;
  
  // Skip adder bytes
  for (int i = 0; i < 4; i++) {
    Serial2.read();
  }
  
  // Read packet data
  byte pid = Serial2.read();
  byte lengthHigh = Serial2.read();
  byte lengthLow = Serial2.read();
  byte confirmationCode = Serial2.read();
  byte param1 = Serial2.read();
  byte param2 = Serial2.read();
  
  // Skip remaining bytes (checksum) to clear the packet
  for (int i = 0; i < 2; i++) {
    Serial2.read();
  }
  
  // Update step and entry tracking
  *step = param1;
  if (param2 < 0xF0) {
    *entry = param2;
  }
  
  // Only process the packet if it's an acknowledge packet (pid == 0x07)
  if (pid != 0x07) {
    return true; // Not an error, just not the packet we process
  }
  
  // Process response based on confirmation code and parameters
  switch (confirmationCode) {
    case 0x00:
      printEnrollmentStatus(param1, param2);
      break;
      
    case 0x0B:
      enrollError = true;
      Serial.println("Error: Invalid ID number");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("Invalid ID num");
      timer2Run(2000000);
      return false;
      
    case 0x1F:
      enrollError = true;
      Serial.println("Error: Fingerprint database is full");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("FPR DB full");
      timer2Run(2000000);
      return false;
      
    case 0x22:
      enrollError = true;
      Serial.println("Error: Template already exists for this ID");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("Temp alr exist");
      timer2Run(2000000);
      return false;
      
    case 0x25:
      enrollError = true;
      Serial.println("Error: Invalid number of entries specified");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("Inval entry num");
      timer2Run(2000000);
      return false;
      
    case 0x26:
      enrollError = true;
      Serial.println("Error: Operation timeout");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("Timeout");
      timer2Run(2000000);
      return false;
      
    case 0x27:
      enrollError = true;
      Serial.println("Warning: Duplicate fingerprint detected");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("Duplicate FPR");
      timer2Run(2000000);
      return false;
      
    case 0x28:
      enrollError = true;
      Serial.println("Warning: Finger area overlap detected, please use a different area");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("Fingr area ovrlp");
      timer2Run(2000000);
      return false;
      
    case 0x3B:
      enrollError = true;
      Serial.println("Error: Poor template quality");
      lcd.clear();
      lcd.print("Error");
      lcd.setCursor(0, 1);
      lcd.print("Bad temp quality");
      timer2Run(2000000);
      return false;
  }
  
  return true;
}

void printEnrollmentStatus(byte param1, byte param2) {
  switch (param1) {
    case 0x00:
      Serial.println("Instruction validity check passed");
      lcd.clear();
      lcd.print("Place finger");
      lcd.setCursor(0, 1);
      lcd.print("1st take");
      break;
      
    case 0x01:
      Serial.print("Collecting fingerprint image #");
      Serial.println(param2);
      break;
      
    case 0x02:
      Serial.print("Generating features for capture #");
      Serial.println(param2);
      lcd.clear();
      lcd.print("Imgage captured");
      lcd.setCursor(0, 1);
      lcd.print("Remove finger");
      break;
      
    case 0x03:
      Serial.println("Waiting for finger to be removed");
      lcd.clear();
      lcd.print("Place finger");
      lcd.setCursor(0, 1);
      lcd.print("2nd take");
      break;
      
    case 0x04:
      if (param2 == 0xF0) {
        Serial.println("Template merged successfully");
      }
      break;
      
    case 0x05:
      if (param2 == 0xF1) {
        Serial.println("Registration check complete");
      }
      break;
      
    case 0x06:
      if (param2 == 0xF2) {
        Serial.println("Template stored sucessfully");
        lcd.clear();
        lcd.print("Enroll");
        lcd.setCursor(0, 1);
        lcd.print("Sucess");
        timer2Run(3000000);
      }
      break;
  }
}

// ============================= Fingerprint enroll ==============================


// ============================= Delete fingerprint data ==============================

void emptyFingerprintData() {
  Serial.println("Emptying fingerprint database...");

  fingerIDEnroll = 1;
  Firebase.setInt(firebaseData, "/track/fingerID", fingerIDEnroll);

  Serial2.write((uint8_t)0xEF);
  Serial2.write((uint8_t)0x01); // Header
  for (int i = 0; i < 4; i++) {
    Serial2.write((uint8_t)0xFF);
  } // Module address
  Serial2.write((uint8_t)0x01); // Package identifier
  Serial2.write((uint8_t)0x00);
  Serial2.write((uint8_t)0x03); // Package length
  Serial2.write((uint8_t)0x0D); // Instruction code
  Serial2.write((uint8_t)0x00);
  Serial2.write((uint8_t)0x11); // Checksum

  Serial.println("Waiting for response...");
  
  // Clear any existing data in the buffer
  while(Serial2.available()) {
    Serial2.read();
  }
  
  // Wait for response with timeout
  unsigned long startTime = millis();
  const unsigned long timeout = 5000;
  
  while (millis() - startTime < timeout) {
    if (Serial2.available() >= 12) {
      // Look for the acknowledge packet (0x07)
      byte buffer[12];
      for(int i = 0; i < 12; i++) {
        buffer[i] = Serial2.read();
      }
      
      // Check if this is an acknowledge packet
      if (buffer[0] == 0xEF && buffer[1] == 0x01 && buffer[6] == 0x07) {
        byte confirmationCode = buffer[9];
        
        if (confirmationCode == 0x00) {
          Serial.println("Database successfully emptied!");
          return; // Success
        } else {
          delFingerError = true;
          Serial.print("Error clearing database. Code: 0x");
          Serial.println(confirmationCode, HEX);
          return;
        }
      }
    }
    delay(10);
  }
  
  Serial.println("Error: Timeout waiting for response");
  delFingerError = true;
}

// ============================= Delete fingerprint data ==============================


void ioStatePolling(){
  
  Firebase.getBool(firebaseData, "/track/autoMode");
  autoMode = firebaseData.boolData();

  if (autoMode == false){
  Firebase.getBool(firebaseData, "/ioState/doorState");
  doorState = firebaseData.boolData();
  
  Firebase.getBool(firebaseData, "/ioState/fan1State");
  fan1State = firebaseData.boolData();
  
  Firebase.getBool(firebaseData, "/ioState/fan2State");
  fan2State = firebaseData.boolData();
  
  Firebase.getBool(firebaseData, "/ioState/light1State");
  light1State = firebaseData.boolData();
  
  Firebase.getBool(firebaseData, "/ioState/light2State");
  light2State = firebaseData.boolData();
  }

  Firebase.getBool(firebaseData, "/track/enrollFlag");
  enrollFlag = firebaseData.boolData();

  Firebase.getBool(firebaseData, "/track/delFingerprintData");
  delFingerprintData = firebaseData.boolData();

  Serial.printf("Data polled from DB\n");
  Serial.printf("Loads' state: fan1 = %d ; fan2 = %d ; light1 = %d ; light2 = %d ; enrollFlag = %d\n", fan1State, fan2State, light1State, light2State, enrollFlag);
  doorLock(doorState);
}

void loadControl(){
  if (autoMode){ // Auto mode
    Serial.println("Current mode: auto");
    if (photo <= 30){
      digitalWrite(light1Pin, HIGH);
      digitalWrite(light2Pin, HIGH);
      Firebase.setBool(firebaseData, "/ioState/light1State", true);
      Firebase.setBool(firebaseData, "/ioState/light2State", true);}
    else if (photo <= 70){
      digitalWrite(light1Pin, HIGH);
      digitalWrite(light2Pin, LOW);
      Firebase.setBool(firebaseData, "/ioState/light1State", true);
      Firebase.setBool(firebaseData, "/ioState/light2State", false);}
    else {
      digitalWrite(light1Pin, LOW);
      digitalWrite(light2Pin, LOW);
      Firebase.setBool(firebaseData, "/ioState/light1State", false);
      Firebase.setBool(firebaseData, "/ioState/light2State", false);}

    if (temp <= 28){
      digitalWrite(fan1Pin, LOW);
      digitalWrite(fan2Pin, LOW);
      Firebase.setBool(firebaseData, "/ioState/fan1State", false);
      Firebase.setBool(firebaseData, "/ioState/fan2State", false);
    }
    else if (temp > 28 && temp <= 31){
      digitalWrite(fan1Pin, HIGH);
      digitalWrite(fan2Pin, LOW);
      Firebase.setBool(firebaseData, "/ioState/fan1State", true);
      Firebase.setBool(firebaseData, "/ioState/fan2State", false);
    }
    else {
      digitalWrite(fan1Pin, HIGH);
      digitalWrite(fan2Pin, HIGH);
      Firebase.setBool(firebaseData, "/ioState/fan1State", true);
      Firebase.setBool(firebaseData, "/ioState/fan2State", true);
    }
  }
  else { // Manual mode
    Serial.println("Current mode: manual");
    digitalWrite(fan1Pin, fan1State ? HIGH : LOW);
    digitalWrite(fan2Pin, fan2State ? HIGH : LOW);
    digitalWrite(light1Pin, light1State ? HIGH : LOW);
    digitalWrite(light2Pin, light2State ? HIGH : LOW);
  }
}

void loop() {

  if (!lcdRunning){
    lcd.clear();
    lcd.print("Waiting for");
    lcd.setCursor(0, 1);
    lcd.print("Fingerprint...");
  }

  if (poll_flag){
    ioStatePolling();
    poll_flag = false;
    timerStop(timer0);
    timerWrite(timer0, 0);
    timerStart(timer0);
  }


// ========== Auto/Manual mode ==========
  if (inSession){
  loadControl();
  }
// ========== Auto/Manual mode ==========


// ========== Read and send sensor data ==========
  if (dht_flag){
    read_temp_hum();
    dht_flag = false;
    timerStop(timer1);
    timerWrite(timer1, 0);
    timerStart(timer1);
  }
// ========== Read and send sensor data ==========


// ========== Identify fingerprint ==========
  if (digitalRead(scannerIntPin) == HIGH && enrollFlag == false){
    timerStop(timer0);
    timerStop(timer1);
    AutoIdentify();
    timerStart(timer0);
    timerStart(timer1);
  }
// ========== Identify fingerprint ==========


// ========== Enroll new fingerprint ==========
  if (enrollFlag){
    timerStop(timer0);
    timerStop(timer1);

    enrollFlag = false;
    Firebase.getInt(firebaseData, "/track/fingerID");
    fingerIDEnroll = firebaseData.intData();

    Serial.printf("Entering enroll mode\n");
    autoEnroll();

    if (enrollError == false){
      Firebase.setBool(firebaseData, "/track/enrollDone", true);
      Firebase.setBool(firebaseData, "/track/enrollFlag", false);
      Firebase.setInt(firebaseData, "/track/fingerID", fingerIDEnroll);}

    else {
      Firebase.setBool(firebaseData, "/track/enrollFlag", false);
      Firebase.setBool(firebaseData, "/track/enrollError", true);
    }

    enrollError = false;

    timerStart(timer0);
    timerStart(timer1);
  }
// ========== Enroll new fingerprint ==========


// ========== Delete fingerprint data ==========
  if (delFingerprintData){
    Serial.printf("Deleting fingerprint DB\n");

    emptyFingerprintData();

    delFingerprintData = false;
    Firebase.setBool(firebaseData, "/track/delFingerprintData", delFingerprintData);

    if (delFingerError){
      Firebase.setBool(firebaseData, "/track/delFingerError", delFingerError);
      delFingerError = false;
    } else {
      Firebase.setBool(firebaseData, "/track/delFingerDone", true);
    }
  }
// ========== Delete fingerprint data ==========


  delay(1000);
}