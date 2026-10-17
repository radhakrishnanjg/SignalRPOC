@echo off
rem Starts the local Kafka broker on Windows (no Docker).
rem Before starting it clears the read-only flag on the KRaft metadata checkpoint files and removes
rem leftover *.deleted files: on Windows Kafka cannot delete read-only files and stops at startup
rem with "AccessDeniedException ... .checkpoint.deleted".

set "KAFKA_HOME=C:\kafka"
set "JAVA_HOME=C:\Program Files\Java\jdk-17"
set "PATH=%JAVA_HOME%\bin;%PATH%"
set "META=%KAFKA_HOME%\kraft-combined-logs\__cluster_metadata-0"

if exist "%META%" (
  attrib -R "%META%\*.checkpoint" >nul 2>&1
  attrib -R "%META%\*.deleted" >nul 2>&1
  del /F /Q "%META%\*.deleted" >nul 2>&1
)

cd /d "%KAFKA_HOME%"
.\bin\windows\kafka-server-start.bat .\config\server.properties
