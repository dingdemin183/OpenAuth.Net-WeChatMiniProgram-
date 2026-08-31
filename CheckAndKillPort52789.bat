@echo off
chcp 936
setlocal enabledelayedexpansion
echo 正在检查52789端口占用情况...

rem 查找占用52789端口的进程
FOR /F "tokens=5" %%i IN ('netstat -ano ^| findstr :52789 ^| findstr LISTENING') DO (
    SET pid=%%i
    echo 发现占用52789端口的进程，PID: %%i
    echo 正在查询进程信息...
    tasklist | findstr %%i
    
    echo 正在结束进程 %%i...
    taskkill /F /PID %%i
    
    echo 验证端口是否已释放...
    netstat -ano | findstr :52789
    goto :END
)

echo 未发现占用52789端口的进程。

:END
echo 操作完成。
pause 