"""Wrapper that avoids sandbox psutil/sysctl failures before starting Locust."""

import psutil

psutil.cpu_count = lambda logical=True: 4  # type: ignore[assignment]
psutil.Process.cpu_percent = lambda self, interval=None: 0.0  # type: ignore[method-assign]

from locust.main import main

if __name__ == "__main__":
    main()
