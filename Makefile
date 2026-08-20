.PHONY: run run-front run-back run-vision watch-training

run-front:
	cd frontend && npm run dev

run-back:
	ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/PlateLens.WebApi --urls http://127.0.0.1:5055

vision/apple-ocr: vision/apple_ocr.swift
	swiftc vision/apple_ocr.swift -o vision/apple-ocr

run-vision: vision/apple-ocr
	vision/.venv/bin/uvicorn app:app --app-dir vision --host 127.0.0.1 --port 8001

watch-training:
	tail -f vision/runs/{detector,classifier}/results.csv

run:
	@if lsof -tiTCP:5173 -sTCP:LISTEN >/dev/null 2>&1 || lsof -tiTCP:5055 -sTCP:LISTEN >/dev/null 2>&1 || lsof -tiTCP:8001 -sTCP:LISTEN >/dev/null 2>&1; then \
		echo "PlateLens já está rodando em http://127.0.0.1:5173"; \
		exit 0; \
	fi; \
	trap 'kill $$vision_pid $$back_pid $$front_pid 2>/dev/null || true' INT TERM EXIT; \
	$(MAKE) run-vision & vision_pid=$$!; \
	$(MAKE) run-back & back_pid=$$!; \
	$(MAKE) run-front & front_pid=$$!; \
	wait
