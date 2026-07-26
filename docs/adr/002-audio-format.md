# ADR 002: Audio Format

Use 16 kHz, 16-bit, mono PCM frames and incrementally finalized WAV files. This is natively supported, deterministic, accepted by Azure, and low risk. The 25 MB Azure limit makes long-session compression or segmentation a remaining requirement.
