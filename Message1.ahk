DetectHiddenWindows true

winTitle := "FlyMenuReceiverWindow"
hwnd := WinExist(winTitle)
if !hwnd {
    MsgBox "Receiver window not found."
    return
}

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
    payload := "DESKTOP2"
}

SetTimer SwitchLater, -10
  
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