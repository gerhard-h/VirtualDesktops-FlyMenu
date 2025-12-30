;AutoHotkey v1 version 
exepath := "C:\path\fly\FlyMenu.exe" ;; Change this to the actual path of FlyMenu.exe
exename := "FlyMenu.exe"
DetectHiddenWindows true
winTitle := "FlyMenuReceiverWindow"

; Get command line arguments
payload := ""
if A_Args.Length > 0 {
    ; Join all arguments with spaces
    for index, arg in A_Args {
        if (index > 1)
            payload .= " "
      payload .= arg
    }
} else {
    ; Default payload if no arguments provided
    payload := "show"
}

hwnd := WinExist(winTitle)
if !hwnd {
    
    if( payload == "start") {
       Run A_ScriptDir . "\" . exename
       return
    } else {
        MsgBox "Receiver window not found."
        return
    }
}

if (payload == "stop" or payload == "exit" or payload == "quit") {
    Run  "taskkill /f /im " . exename
    return
}

SetTimer SwitchLater, -100
  
SwitchLater() {
    global payload  ; Access the outer payload variable
    buf := Buffer(StrPut(payload, "UTF-8"))
    StrPut(payload, buf, "UTF-8")
    cds := Buffer(A_PtrSize*3, 0)
    NumPut("UPtr", 0,   cds, 0)
    NumPut("UPtr", buf.Size,     cds, A_PtrSize)
    NumPut("UPtr", buf.Ptr,      cds, A_PtrSize*2)
    try{
        SendMessage 0x4A, 0, cds.Ptr,, "ahk_id " hwnd
    }
}