#!/usr/bin/env python3
"""Testa MySQL real isolado no macOS/Linux, sem Docker ou dados existentes."""
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import time
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
TESTS = "tests/Workoutly.Api.Tests/Workoutly.Api.Tests.csproj"


def stop(process):
    if process and process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)


def wait_http(url, expected_status, expected_database):
    for _ in range(100):
        try:
            try:
                response = urlopen(url, timeout=2)
            except HTTPError as error:
                response = error
            with response:
                status = response.status
                body = json.load(response)
            if status == expected_status and body.get("database") == expected_database:
                print(f"HTTP {status}: database={body['database']}", flush=True)
                return
        except (URLError, OSError):
            pass
        time.sleep(0.2)
    raise RuntimeError("A API não atingiu a prontidão esperada; veja a configuração MySQL.")


def main():
    if os.name == "nt":
        raise SystemExit("No Windows, use o ambiente Docker descrito em docs/mysql.md.")
    mysqld, mysql = shutil.which("mysqld"), shutil.which("mysql")
    local_dotnet = Path.home() / ".dotnet" / "dotnet"
    dotnet = os.environ.get("DOTNET_COMMAND") or (str(local_dotnet) if local_dotnet.is_file() else shutil.which("dotnet"))
    if not all([mysqld, mysql, dotnet]):
        raise SystemExit("Requer mysqld, mysql e SDK .NET instalados.")
    subprocess.run([mysqld, "--version"], check=True)
    with tempfile.TemporaryDirectory(prefix="wly-mysql-", dir="/tmp") as directory:
        folder = Path(directory)
        data, mysql_socket = folder / "data", folder / "mysql.sock"
        server = api = None
        with (folder / "server-output.log").open("w") as database_log, (folder / "api.log").open("w") as api_log:
            try:
                print("Inicializando instância temporária sem TCP...", flush=True)
                subprocess.run([mysqld, "--no-defaults", "--initialize-insecure", f"--datadir={data}"],
                               stdout=database_log, stderr=subprocess.STDOUT, check=True, timeout=60)
                server = subprocess.Popen([
                    mysqld, "--no-defaults", f"--datadir={data}", f"--socket={mysql_socket}",
                    f"--pid-file={folder / 'mysql.pid'}", "--skip-networking", "--mysqlx=OFF",
                    "--innodb-buffer-pool-size=64M"
                ], stdout=database_log, stderr=subprocess.STDOUT)
                mysql_command = [mysql, "--no-defaults", "--protocol=SOCKET", f"--socket={mysql_socket}", "-uroot"]
                for _ in range(150):
                    if server.poll() is not None:
                        raise RuntimeError("MySQL temporário encerrou antes de iniciar.")
                    result = subprocess.run([*mysql_command, "-Nse", "SELECT 1"], capture_output=True)
                    if result.returncode == 0:
                        break
                    time.sleep(0.2)
                else:
                    raise RuntimeError("MySQL temporário não iniciou no prazo.")
                subprocess.run([*mysql_command, "-e", "CREATE DATABASE workoutly_validation CHARACTER SET utf8mb4"], check=True)
                environment = os.environ.copy()
                admin = f"Server={mysql_socket};User ID=root;SslMode=Disabled;Pooling=false"
                environment["WORKOUTLY_TEST_ADMIN_CONNECTION"] = admin
                environment["ConnectionStrings__Workoutly"] = admin + ";Database=workoutly_validation"

                subprocess.run([dotnet, "test", TESTS, "--logger", "console;verbosity=minimal"], cwd=ROOT, env=environment, check=True)
                # Kestrel real: falta de esquema, comando de migração e leitura após reiniciar a API.
                with socket.socket() as available:
                    available.bind(("127.0.0.1", 0))
                    port = available.getsockname()[1]
                # Iniciar o assembly diretamente: o PID controlado é o servidor, sem processo filho do dotnet run.
                api_dll = ROOT / "src/Workoutly.Api/bin/Debug/net10.0/Workoutly.Api.dll"
                launch = [dotnet, str(api_dll), "--urls", f"http://127.0.0.1:{port}"]
                url = f"http://127.0.0.1:{port}/health/ready"
                api = subprocess.Popen(launch, cwd=ROOT, env=environment, stdout=api_log, stderr=subprocess.STDOUT)
                wait_http(url, 503, "schema_pending")
                subprocess.run([dotnet, str(api_dll), "--migrate"],
                               cwd=ROOT, env=environment, check=True)
                wait_http(url, 200, "ready")
                stop(api)
                api = subprocess.Popen(launch, cwd=ROOT, env=environment, stdout=api_log, stderr=subprocess.STDOUT)
                wait_http(url, 200, "ready")
                print("Persistência e reinício da API verificados. Nenhum banco existente foi utilizado.", flush=True)
            finally:
                stop(api)
                stop(server)
    print("Instância temporária encerrada e arquivos removidos.", flush=True)


if __name__ == "__main__":
    main()
