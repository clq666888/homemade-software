"""Per-input credentials are private job data, never command-line arguments."""
from pypdf import PdfReader

def password_for(job,source):
    return job.get('passwords',{}).get(str(source),job.get('password',''))

def probe(path,password=''):
    reader=PdfReader(path)
    try:
        encrypted=reader.is_encrypted
        return {'encrypted':encrypted,'needsPassword':encrypted and not bool(reader.decrypt(password))}
    finally:reader.close()
