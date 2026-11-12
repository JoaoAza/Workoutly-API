#!/usr/bin/env python3
"""MySQL local isolado e API para desenvolver no Mac/Linux, sem Docker."""
import os
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]


def stop(process):
    if process and process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)


def main():
    mysqld, mysql = shutil.which("mysqld"), shutil.which("mysql")
    local_dotnet = Path.home() / ".dotnet" / "dotnet"
    dotnet = os.environ.get("DOTNET_COMMAND") or (str(local_dotnet) if local_dotnet.is_file() else shutil.which("dotnet"))
    if os.name == "nt" or not all([mysqld, mysql, dotnet]):
        raise SystemExit("Este helper requer macOS/Linux, MySQL nativo e .NET. No Windows, use Docker Compose.")
    if not (ROOT / ".env").exists():
        subprocess.run([sys.executable, str(ROOT / "scripts/dev.py"), "init"], check=True)
    values = dict(line.split("=", 1) for line in (ROOT / ".env").read_text().splitlines() if line and not line.startswith("#"))
    password = values["MYSQL_PASSWORD"]
    # Valor gerado no .env; nunca passado na linha de comando ou impresso.
    sql_password = password.replace("\\", "\\\\").replace("'", "''")
    data = ROOT / ".local/native-mysql/data"
    runtime_file = data.parent / "runtime.json"
    data.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([dotnet, "build", "src/Workoutly.Api/Workoutly.Api.csproj"], cwd=ROOT, check=True)
    with tempfile.TemporaryDirectory(prefix="wly-dev-", dir="/tmp") as folder:
        socket = Path(folder) / "mysql.sock"
        server = api = None
        with (data.parent / "server.log").open("a") as log:
            try:
                if not data.exists():
                    subprocess.run([mysqld, "--no-defaults", "--initialize-insecure", f"--datadir={data}"],
                                   stdout=log, stderr=subprocess.STDOUT, check=True)
                server = subprocess.Popen([mysqld, "--no-defaults", f"--datadir={data}", f"--socket={socket}",
                    f"--pid-file={Path(folder) / 'mysql.pid'}", "--skip-networking", "--mysqlx=OFF"], stdout=log, stderr=subprocess.STDOUT)
                command = [mysql, "--no-defaults", "--protocol=SOCKET", f"--socket={socket}", "-uroot"]
                for _ in range(150):
                    if server.poll() is not None:
                        raise RuntimeError("MySQL não iniciou. Confira .local/native-mysql/server.log; outra instância pode estar usando os dados.")
                    if subprocess.run([*command, "-Nse", "SELECT 1"], capture_output=True).returncode == 0:
                        break
                    time.sleep(0.2)
                else:
                    raise RuntimeError("MySQL não iniciou no prazo.")
                sql = ("CREATE DATABASE IF NOT EXISTS workoutly CHARACTER SET utf8mb4;"
                       f"CREATE USER IF NOT EXISTS 'workoutly'@'localhost' IDENTIFIED BY '{sql_password}';"
                       "GRANT ALL PRIVILEGES ON workoutly.* TO 'workoutly'@'localhost';")
                subprocess.run(command, input=sql, text=True, capture_output=True, check=True)
                credential_environment = os.environ.copy()
                credential_environment["MYSQL_PWD"] = password
                restricted = subprocess.run([mysql, "--no-defaults", "--protocol=SOCKET", f"--socket={socket}", "-uworkoutly", "-Nse", "SELECT 1"],
                    env=credential_environment, capture_output=True)
                if restricted.returncode != 0:
                    raise RuntimeError("A senha do .env não corresponde à conta MySQL local já criada. Nenhuma senha foi alterada.")
                environment = os.environ.copy()
                # Escape da senha no formato de connection string, que aceita ponto e vírgula e aspas.
                quoted_password = '"' + password.replace('"', '""') + '"'
                environment["ConnectionStrings__Workoutly"] = f"Server={socket};Database=workoutly;User ID=workoutly;Password={quoted_password};SslMode=Required"
                environment["ASPNETCORE_ENVIRONMENT"] = "Development"
                dll = ROOT / "src/Workoutly.Api/bin/Debug/net10.0/Workoutly.Api.dll"
                subprocess.run([dotnet, str(dll), "--migrate"], cwd=ROOT, env=environment, check=True)
                runtime_file.write_text(json.dumps({"socket": str(socket)}), encoding="utf-8")
                runtime_file.chmod(0o600)
                api = subprocess.Popen([dotnet, str(dll), "--urls", "http://localhost:5080"], cwd=ROOT, env=environment)
                print("Workoutly disponível em http://localhost:5080. Ctrl+C encerra os serviços e preserva os dados em .local/.", flush=True)
                return api.wait()
            except KeyboardInterrupt:
                print("\nEncerrando serviços; dados preservados.", flush=True)
                return 0
            finally:
                stop(api)
                stop(server)
                if runtime_file.exists() and json.loads(runtime_file.read_text()).get("socket") == str(socket):
                    runtime_file.unlink()


if __name__ == "__main__":
    sys.exit(main())
