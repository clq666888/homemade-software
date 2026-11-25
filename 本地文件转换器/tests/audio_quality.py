"""Independent signal check: retained tone and quiet-section noise reduction."""
import sys,wave,math,array
from pathlib import Path
root=Path(sys.argv[1]);source=root/'中文 噪声样本.wav';output=next((root/'denoised').glob('*.wav'))
def samples(path):
    with wave.open(str(path),'rb') as stream:
        rate=stream.getframerate();channels=stream.getnchannels();data=array.array('h',stream.readframes(stream.getnframes()))
    return rate,[x/32768 for x in data[::channels]]
rate,before=samples(source);_,after=samples(output)
def rms(values):return math.sqrt(sum(x*x for x in values)/len(values))
before_noise=rms(before[rate//2:rate]);after_noise=rms(after[rate//2:rate]);gain=rms(after[2*rate:3*rate])/rms(before[2*rate:3*rate])
print('quiet_noise_rms_before=',before_noise,'after=',after_noise,'tone_rms_ratio=',gain)
assert after_noise<before_noise*0.85,'stationary noise not reduced sufficiently'
assert .75<gain<1.15,'desired signal excessively changed'
print('PASS quieter stationary noise and retained tone energy (synthetic fixture, not a universal quality claim)')
